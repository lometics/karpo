# Resilience: Karpo through everything Windows does to it

The user (2026-10-30): "karpo itself i want to be very very stable … adding screen, removing
screen, full screen apps, different resolution screens, power settings, sleep mode, hibernate
mode. karpo has to function in all of those scenarios."

This is the strategy for testing that, what the first run found, and what was fixed.

## 1. What "functions" means

After anything Windows or the person does, and within a few seconds of it settling:

1. **The same dock is still running**, and it answers its windows' messages (no crash, no hang,
   no restart by the guard).
2. **Every display that had a bar has one**: on the same edge, the full length of it, inside the
   display, at the display's scale now.
3. **The work area leaves the bar its strip, exactly**: windows never go under the bar, and no
   gap is left over it.
4. **Explorer's taskbar never shows** while Karpo runs, on any display (widgets.md §465).
5. **Nothing of Karpo's is in front of a full screen app**, and the bars come back when it goes.
6. **Karpo logs no fault** (karpo.log has no "caught and carried on", no fatal error, no guard
   restart).
7. **It holds nothing more after the change than before**: handles, GDI and USER objects,
   memory and threads stay flat over repeated changes.
8. **It never takes the foreground by itself**: the app in front keeps the keyboard.
9. **No tooltip, window list or menu of Karpo's stays shown once the pointer is off Karpo**:
   gone within a second, however the pointer left.
10. **Above its strip a bar lets every press through**: the bar's window is taller than the bar,
    and no point of it outside the strip may take the pointer from the apps under it.
11. **It opens and closes nothing by itself**: no process started by the dock, no other app's
    window gone (one gone during a scenario is named in the report).
12. **It costs nothing when nothing happens, and little when something does**: under one
    percent of one core idle (the clock and the live graphs); it stays answering on a machine whose every core is busy.

Rules 8–11 are judged on the way, not only at the end: each has a patience
(`Rules.Patience`) — a popup left a second, a bar over the apps half of one, the foreground taken
not even once.

Brief breaks while a change is under way (a bar re-laid out for 100–800 ms after a mode change)
are expected and reported with their length; a rule still broken after the settle is a failure.

## 2. Four layers of tests

| Layer | Where | What it proves | Runs |
|---|---|---|---|
| **1. A made-up desktop** | `Karpo.Tests/Docking/BarWorld.cs`, `BarKeeperTests`, `BarKeeperPropertyTests` | The one reconcile (BarKeeper, §272) against any sequence: displays unplugged, modes changed, full screen games, Explorer restarts or stalls or fights, dark screens. 1000 random 60-step runs, each from a seed. | every `dotnet test` |
| **2. The real scene** | `Karpo.Tests/Signals/SignalSceneTests.cs` and the other scene tests | The real composition tree laid out again at another scale, parts and tiles gone and back, without an exception and with everything where it belongs. | `KARPO_UI_TESTS=1` |
| **3. What a message does** | `Karpo.Tests/Docking/DeferredTests.cs`, `EnergySaverTests` | Which of Windows' notices survive a modal loop (a menu or dialog open while they come). Pure functions of the dock's message handling. | every `dotnet test` |
| **4. The live desktop** | `Karpo.Resilience` (this doc, §5) | The real Karpo on the real machine, with the real Explorer, displays and power: each scenario done for real and the rules of §1 checked every 100 ms from outside. | by hand, on a machine the person can lend for a few minutes |

Layers 1–3 are fast and run with every change. Layer 4 is the only one that can find what the
models do not know about (the signal crash below was invisible to layers 1–3 until layer 4 showed
it), so every bug it finds gets a layer 1–3 test that fails without the fix.

## 3. The scenarios

`Karpo.Resilience list` prints them. Each undoes what it did; the runner also puts the displays
back to how the run found them after every scenario, whatever happened.

| Scenario | What happens | How it is done |
|---|---|---|
| idle | nothing, for 20 s | — |
| mode-second / mode-primary | a display to 1920x1080 and back | `ChangeDisplaySettingsEx`, dynamic (never written to the registry) |
| mode-tiny | a display to 800x600, smaller than the bar's content | same |
| rotate-second | a display to portrait and back | same, `DM_DISPLAYORIENTATION` |
| scale-second / scale-primary | the scale a step up and back | `DisplayConfigSetDeviceInfo` type −4 (Settings' own call; the one change Windows keeps, so the scale before is put back) |
| unplug-second / unplug-primary | a display taken away (as its cable pulled) and back; the primary's going makes the other primary | `SetDisplayConfig` with the path left out, without `SDC_SAVE_TO_DATABASE` |
| duplicate | Win+P Duplicate and extend again | `SDC_TOPOLOGY_CLONE` |
| swap-primary | the other display becomes primary and back | every source moved so it stands at 0,0 |
| displays-off | every display off for 15 s, lit by the pointer | `SC_MONITORPOWER`, then `SendInput` |
| fullscreen-borderless | a borderless window covering a display (a video, a borderless game) | a test window in a child process, brought to the front with a click |
| fullscreen-exclusive | a Direct3D exclusive full screen app at 1280x720 (the mode changes) | a blt-model swap chain, `SetFullscreenState` |
| maximized | a maximized window | the window must stay inside the work area |
| explorer-restart | Explorer ends as in a crash; winlogon starts it again | `Process.Kill` on the explorer.exe that owns the taskbar |
| guard-killed | Karpo's guard ended from outside (Task Manager) | the dock must start another within 5 s |
| dock-killed | the dock ended as by a crash or End task | Windows' taskbar shown, not auto-hidden, within 2 s; the guard starts Karpo again, its bars up and the taskbar hidden, within 40 s |
| graphics-reset | the graphics driver restarts | Win+Ctrl+Shift+B by `SendInput` (it does work injected) |
| storm | two modes, a scale, a display away and back, within 3 s | faster than any settle |
| hover-leave | the pointer rests on up to six buttons until a tooltip or window list shows, then leaves each five ways: a jump with no move between (a touchpad, a remote session, a pen lifted), to the other display, a flick, across the tooltip, off the bar's end | `SetCursorPos`, `SendInput`; found by walking the strip |
| hover-leave-busy | the same with every core busy | |
| cpu-storm | every core busy for 30 s (two busy threads a core, normal priority) | a child process; Karpo may use 1 % of one core |
| cpu-storm-changes | every core busy while a display changes mode and goes and comes back | |
| memory-pressure | 85 % of the free memory taken and touched for 25 s | a child process |
| hung-app | an app with a taskbar button stops answering (Windows ghosts it after 5 s) | its thread sleeps for ever |
| title-storm | an app renames its window 100 times a second | Karpo may use 5 % of one core |
| progress-storm | an app's button shows progress 20 times a second, an overlay coming and going, while its display changes scale and mode | `ITaskbarList3`; 8 % |
| window-flood | an app opens 150 windows at once and closes them 4 s later | USER objects and handles back after |
| sleep | S3 for 45 s, woken by a timer | `SetWaitableTimer(fResume)` then `SetSuspendState` |
| hibernate | S4 for 90 s, woken by a timer | same, hibernate |
| start-placement | Windows' Start opened by each bar's Start button (three quick reopenings too), and with `--keys` by the Windows key and Ctrl+Esc: it must stand where Karpo places it (widgets.md §666) — off the bar by Windows' gap, centred on the button, on its display — measured with pixel deltas and a picture each; by name only, never in `run all` | UI Automation's Invoke on the bar's Start element (the button's own click); `SendInput` only with `--keys` |

Not automated, because they need a person's credentials or admin: **lock and unlock**, **fast
user switching**, **a remote desktop session** (each reaches Karpo as `WM_WTSSESSION_CHANGE` plus
display changes, which layers 1 and 3 cover), **killing dwm.exe**, **a forced GPU timeout**
(`dxcap -forcetdr`), **a virtual display driver** for displays this machine does not have. Run
those by hand with `Karpo.Resilience watch 120` going: it reports every rule broken and for how
long.

## 4. What Karpo does about each, and the rules it keeps

- **One reconcile** (BarKeeper, §272) decides the bars, the strips and Explorer's taskbar, on
  events only: display change, scale change, resume, unlock, display lit, Explorer back, work
  area changed, the window in front. A display with a full screen app is never touched.
- **What arrives during a modal loop is kept, not lost** (`Dock.Deferrable`). A menu or dialog
  open is when the dock is busy; a notice the dock does not keep is gone for good. Kept: display
  and scale changes, setting and theme changes, the session's lock and unlock, Explorer's restart,
  and **resume** — a menu left open over a sleep must not skip the clock, the alarms, the islands
  and the strips. A notice whose lParam points at data keeps none of it (`Dock.Kept`).
- **A low-level hook is put in again after a wake and an unlock.** Windows takes a hook away
  without a word once it answered too slowly (`LowLevelHooksTimeout`, 1 s), and a wake — pages
  coming back from disk — is when that happens. The keyboard hook (Win+N, the taskbar's chords)
  is put in again at every arrange, every resume and every unlock.
- **Nothing waits for the wall clock to catch up** (`WallClock.Since`). After a hibernation the
  clock can be set back; a fetch time in the future counts as long ago, so the weather, the air,
  the tides, the markets and the feeds are due again rather than waiting hours.
- **A visual never asks a closed parent anything.** Windows.UI.Composition answers RO_E_CLOSED
  to a visual whose parent was closed. What hangs in containers it does not own (an app button's
  progress and corner) keeps where it hangs, and is taken out before that container is closed
  (`Scene.LetSignalsGo`).
- **A hover ends when the pointer is gone, whatever messages come** (`DockWindow.HoverWatched`).
  The bar's tooltip and window list used to hide only on messages: `WM_MOUSELEAVE` (asked for once,
  as the pointer came onto the bar), a move off the panel, the window list's own watch. A leave
  dropped while a modal loop had the dock busy, one lost to a resize of the bar, a tap (no leave
  ever comes) left `hovering` true for good — and with it every later tooltip on that bar, since
  the leave was never asked for again. Now, while a bar is hovered, it asks Windows every 250 ms
  where the pointer is (`GetCursorPos`, `WindowFromPoint`): on the bar's panel, its tooltip or the
  window list it opened, it asks for the leave again; anywhere else the hover ends. The window
  list's watch asks which window is under the pointer too: by rectangles alone a pointer on a
  display beyond the bar's outer edge was on the button.
- **Idle, slow on purpose; a change, at full speed.** The dock runs in EcoQoS while nobody is on
  it (`Efficiency`), so it costs nothing — but a change of displays was then answered on an
  efficiency core at low clock, and with every core busy the bar was missing for 3.3 s and the
  dock did not answer for 3 s. Now a change lifts EcoQoS until the arrange is done
  (`RequestArrange` … `Rest`), and the dock's own thread runs above normal priority: it only runs
  to answer a change or the person, so idle it still costs nothing.
- **A fetch that failed is tried again the moment the network is back** (`OnlineUpdates.RetryNow`,
  `Dock.Network.cs`), not after its retry wait: after a wake the first fetch often runs before the
  Wi-Fi is up.
- **Time for intervals is the tick count, time for "when" is the wall clock.** Since Windows 8 a
  relative timer does not count the time asleep: a timer due in 10 minutes that sleeps for an
  hour fires 10 minutes after the wake, not at once. What must be right at the wake (the clock,
  the alarms) is put right by the resume itself (`PBT_APMRESUMEAUTOMATIC`, which comes at every
  resume; `PBT_APMRESUMESUSPEND` only once the person does something).

## 5. Running the live harness

```powershell
dotnet build Karpo.Resilience -c Release
$h = "Karpo.Resilience\bin\Release\net10.0-windows10.0.26100.0\win-x64\Karpo.Resilience.exe"
& $h probe                      # Karpo now, and whether it holds against itself
& $h run all                    # every scenario but sleep and hibernate (about 5 minutes)
& $h run sleep hibernate        # the machine goes away for 1–2 minutes each
& $h run start-placement        # Windows' Start opened on the person's screen (widgets.md §666); --keys adds the Windows key and Ctrl+Esc
& $h run unplug-second scale-second --cycles 4   # repeated: a leak shows as a climb
& $h watch 120                  # just watch, while a person does something by hand
& $h restore                    # displays back, if a run was cut short
```

The report goes to `artifacts/resilience/run-<time>.md`: per scenario the verdict, every rule
broken on the way and for how long, what Karpo held before and after, and what it wrote to
karpo.log and appbar.log meanwhile.

Before running: the machine's displays will change and the screen will go black for moments; a
sleep needs wake timers allowed in the power plan (they are on AC by default). Only one party
restarts the bar at a time: a publish during a run shows as INTERRUPTED, not as a failure.

The harness sees the bar from outside: the bar's window is taller than the bar (it keeps room
for what opens over it), so the bar's thickness is read from the strip the work area leaves it.
Karpo makes a `Shell_TrayWnd` of its own (the tray that hears the icons); Explorer's taskbar is
the one an explorer.exe owns.

## 6. The first run (2026-10-30), on two 3840x2160 displays at 150 %

| Scenario | Before the fixes | Time to hold |
|---|---|---|
| mode-second, mode-primary, mode-tiny, rotate | FAIL: fault logged | 0.4–0.8 s |
| scale-second, scale-primary | FAIL: fault logged | 0.5–0.8 s |
| unplug-second, unplug-primary, duplicate, swap-primary | PASS | 0.1–0.4 s |
| displays-off | PASS | 3.7 s (the displays themselves take that long) |
| fullscreen-borderless, fullscreen-exclusive | PASS: the bar made way in 75 ms, back within 1 s | |
| maximized, explorer-restart (3.3 s), graphics-reset (1.5 s) | PASS | |
| storm | FAIL: fault logged | |
| sleep (S3), hibernate (S4) | FAIL: fault logged; otherwise held 120–140 ms after the wake | |
| 8 cycles of unplug and scale | no leak: handles, GDI, USER, threads flat | |

**Found and fixed:**

1. **The bar threw at every change of display mode or scale, after every sleep and hibernate**
   (`ObjectDisposedException` from `Scene.PlaceSignals` and `Scene.Sweep`; 208 times in the
   person's karpo.log in two days). An app button's progress run and badge hang in containers the
   scene closes and makes anew when the bar is laid out at another scale: the icon's sprite and
   the part's layer. Reading `.Parent` of a visual whose parent was closed throws, and the rest of
   that update was skipped — the bar half laid out until the next one, the signal's visuals never
   closed. Now the signals keep their own homes and are let go before a home closes.
   Test: `SignalSceneTests.SignalsFollowTheirTileThroughARescaleAndGoWithIt` (failed before).
2. **A scale change or a resume during an open menu was lost** (`WM_DPICHANGED` and
   `PBT_APMRESUMEAUTOMATIC` were not deferrable): the bar kept the old scale's size, and a sleep
   with a menu open skipped the clock, alarms, islands and strips. Test: `DeferredTests`.
3. **The keyboard hook was only put back by an arrange**: after a wake or an unlock in which
   Windows had taken it away, Win+N and the chords stayed dead. Now also on resume and unlock.
4. **A wall clock set back left online widgets waiting** for it to catch up (weather, air, tides,
   markets, feeds). Test: `RssStoreTests.AClockSetBackLeavesNothingWaitingForIt`.

**Seen, not a fault of Karpo's, or not yet fixed:**

- **Explorer's taskbar shows itself for 130–240 ms** after every display or scale change and
  after a graphics reset, before Karpo hides it again ("showed itself: hidden again" in
  appbar.log). Explorer re-shows its taskbars as it lays them out on the new displays; Karpo
  cannot hide another process's window before it is shown. Under Karpo's own bar for most of it.
  Since widgets.md §542 a taskbar window Karpo has seen once draws nothing when it shows (an empty
  window region, also for the few-ms shows Explorer makes every few seconds as it lays itself
  out); only a window Explorer makes anew can show before the hook blanks it. The rules read the
  region (`WindowSeen.InSight`).
- ~~The online widgets wait up to their retry (1–5 min) after a wake~~ — fixed 2026-10-31: the
  dock hears an address arrive (`NetworkChange.NetworkAddressChanged`, not
  `NetworkAvailabilityChanged`, which Hyper-V's always-up virtual adapters keep true), lets the
  burst settle 2 s, and says `OnlineUpdates.CameBack()`: every fetch that failed before is due
  (`RetryNow`), the timer stores (mail, calendars, contacts, tasks) arm at once, and every widget is
  polled. A site's own back-off (a 429) is still waited out. Tests: `OnlineUpdatesTests` (the
  network coming back), `RssStoreTests.TheNetworkBackRetriesAFailureButNotASiteThatSaidWait`.
- **No device-loss handling** (`RenderingDeviceReplaced`, `DXGI_ERROR_DEVICE_REMOVED`): with WARP
  (the default) a graphics driver restart did not lose anything; with `--gpu` it may. Kill
  dwm.exe or force a GPU timeout (admin) to know.

### The second run (2026-10-31): the pointer, a slow machine, apps that misbehave

On the build before the fixes above (the live one).

| Scenario | Result |
|---|---|
| idle | PASS: 0.35 % of one core |
| cpu-storm | PASS: Karpo used 0.5 % of one core while every core was busy, and answered throughout |
| memory-pressure (36 GB of 42 free taken) | PASS |
| hung-app | PASS: nothing hung, nothing closed |
| title-storm | PASS: 1.9 % of one core |
| window-flood | PASS: everything let go |
| cpu-storm-changes | FAIL: the bar missing 3.3 s after a mode change, the dock not answering for 3 s (EcoQoS; fixed) |
| progress-storm | FAIL: the signal crash (fixed); after it **the bar's window took presses 14 px above its strip for 10 s** at 175 % — the crash skipped the rest of the update, the window's region with it — and USER objects +23, memory +32 MB (the signals' visuals never closed). Both should be gone with the signal fix; to confirm on the new build. |
| hover-leave | not run yet: it moves the pointer, and needs the person away from the machine |

**Found from reading the code** (an audit of every way a tooltip or window list is hidden, while
the person's "the tooltips did not hide after I moved the mouse away … same for the window
preview" was the report): nothing watched the tooltip at all — only messages hid it, and a lost
`WM_MOUSELEAVE` was never asked for again (§4, fixed). Still by design, and the person's to
decide: a window list opened by a **click** (a grouped button) or by the keyboard stays until a
press elsewhere or Esc, as Windows' own does.

### The third run (2026-10-31 02:13), on the build with every fix above

All 29 scenarios, sleep and hibernate included, on two 3840x2160 displays at 150 %:

- **PASS: 28.** Every scenario that failed before passes: modes, scales, rotation, the storm,
  the progress storm (no fault, the bar's window over the apps no more, nothing leaked), every
  core busy during display changes (held in 0.6–1.1 s, was 3.3 s), sleep (held 1.4 s after the
  wake) and hibernate (82 ms), no fault logged anywhere.
- **hover-leave and hover-leave-busy: PASS.** Six tooltips on the second display, each left five
  ways: every one gone within 180 ms (160 ms with every core busy).
- **idle: 0.07–0.78 % of one core** over three runs (the clock and the live graphs); the limit is
  1 %.
- One run of `duplicate` saw a full-screen folder of Karpo's take the foreground after the
  displays extended again; three runs more did not, and the person had a folder open on that
  display minutes before — taken as theirs.

## 8. The person's data: never lost, never half shown, never shared by a copy (2026-10-31)

The person: *"in terms of data display and syncing it needs to be bullet proof. i dont want people
complaining that not all of their data shows or that data gets inconsistent or that a settings file
get overwritten or that widgets behave weirdly because they got copied etc."*

An audit of every file Karpo writes, every sync and every copy found config.json's own write sound
(a temporary file through to the disk, `File.Replace` with `.bak`, the lock, the three-way merge,
recovery, a multi-process stress test) and these gaps, all fixed with tests that fail without them:

| Where | What could happen | Now |
|---|---|---|
| accounts.json read while another program held it | taken for "no accounts": every account signed out at the next token write, every cache purged, unsent To Do edits lost | a read says Ok / Missing / Busy / Broken; nothing is written on Busy; the stores keep their accounts unless the file read cleanly; a busy file is read again 2 s on |
| accounts.json, the PIM caches, the islands' caches written | no flush, no backup: a power cut left a zero-byte file and the person signed out | one durable write for all (`DurableFile`: temporary file, write-through, `File.Replace` with `.bak`); an empty or cut-short file reads as its `.bak`; a save cut between the two renames reads as its `.bak` too |
| a layout duplicated | its folders' widgets kept their ids: a timer started in one ran in both, the copy's alarm never rang at its own time | one copy rule gives everything inside new ids; `Repair` keeps every id unique across all layouts (the first keeps it), healing a file already so; cut then paste is a move and keeps a running timer |
| one calendar or address book refused (shared, revoked, 5xx) | the whole account failed, and stayed stale for good | each calendar and book on its own: the healthy ones refresh, the failed one keeps what it had, marked, its reason on the Accounts row |
| a To Do change the server answered badly (5xx, long 429, not JSON) | dropped silently; an Add whose answer was lost made twice | dropped only on a definite refusal (4xx but 408/429); an Add tried once is looked for before it is sent again, across restarts |
| silent caps | CalDAV "507 truncated" read as complete; Graph's 20,000; To Do's 2,000 open; mail's 30 newest across all inboxes (a quiet one showed none); COUNT rules given up after 20,000 periods; events that would not parse dropped | 507 split until complete, or marked; "not everything could be read" said where anything is cut; each inbox keeps its newest five; COUNT rules counted to the window; dropped events counted and said |
| all-day Microsoft events east of UTC | a day early when Graph converts their midnight to UTC | read back in the event's own zone |
| a save the file would not take (held, newer Karpo) | gone after a restart, without a word | kept and retried (1 s doubling to 60 s, at session end, at exit); said on the edit bar and, after three failures, on the bar's menu; a newer Karpo's file is never written |
| config.json held at the start | defaults shown, and later merged into the person's file | defaults stand in unsaved (`Config.Unread`); the file is read again until it reads, and taken as it is |
| a folder edited by the dock and by hand or the CLI at once | the saver's whole list won | each list merged by its widgets' ids |
| a field or a kind a newer build added, read by an older one of the same shape | dropped at the older one's save | every word the file may hold is listed (`Karpo.Tests/Persistence/ConfigWords.txt`); adding one without raising `Config.Shape` fails a test, so a newer build's file always says it is newer, and an older one leaves it alone (shape 38 since 2026-10-31) |

Left as they are, the person's to decide: a copied Note widget shows and edits the same note, and a
copied To-do widget the same list, as two shortcuts to one file do.

## 7. Facts this rests on

- An HMONITOR is valid only while its display stays; Windows hands handles out again after a
  reconfiguration. `WM_DISPLAYCHANGE` says only the primary's size. The order of
  `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `SPI_SETWORKAREA` and the system's own window moves is not
  documented: everything goes into one reconcile, after the burst.
- `PBT_APMSUSPEND` gives about 2 s; `PBT_APMRESUMEAUTOMATIC` comes at every resume. On Modern
  Standby desktop apps are frozen and may get no `WM_POWERBROADCAST` at all:
  `GUID_SESSION_DISPLAY_STATUS` (display lit) is the signal there.
- `GetTickCount64` counts the time asleep; `QueryUnbiasedInterruptTime` does not. Relative
  waitable timers, `SetTimer` and thread pool timers do not count it (Windows 8+).
- A low-level hook is removed without notice when its callback takes longer than
  `LowLevelHooksTimeout` (1 s by default, Windows 10 1709+).
- When the D3D device is lost, the composition graphics device does not replace it by itself:
  the app makes a new D2D device, calls `SetRenderingDevice`, and redraws on
  `RenderingDeviceReplaced`. WARP devices can be removed too (a hardware adapter appearing).
- `SetDisplayConfig` fails with `ERROR_ACCESS_DENIED` off the console (in a remote session).
