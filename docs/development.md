# Building and running Karpo from source

For what Karpo is, see the [README](../README.md); for how changes and releases are made, see
[CONTRIBUTING.md](../CONTRIBUTING.md). This page is for building Karpo, running it from a checkout, and
finding your way around the repository.

Karpo is written in C# and costs next to nothing while you aren't looking at it.
The bar is plain Win32 on the system compositor (Windows.UI.Composition), so
animations run in DWM rather than in this process, and it draws with Direct2D
on the CPU. Its settings are the edit bar's menus, in the bar itself.

## Build and run

Requirements: Windows 10 2004 or later (Windows 11 recommended) and the
.NET 10 SDK; for a published build also Visual Studio's C++ build tools
("Desktop development with C++", or the Build Tools with the C++ tools),
which native AOT links with.

### Publish (to use it)

```powershell
./scripts/publish.ps1
```

This makes `artifacts\publish\Karpo`, a folder that runs on any x64 PC without
.NET installed:

- `Karpo.exe`, the bar, compiled ahead of time to one native executable
  (about 15 MB). It shows its bar about 0.2 s after starting, where a JIT
  build takes 0.4 s and over a second on its first start, and needs less
  memory (about 19 MB private and 53 MB working set at start, against 26 and
  95 MB).

Start `Karpo.exe`. Right-click the bar and choose **Edit taskbar**: the edit bar
along the top holds every setting — the display's layout, **Taskbar**, **Folders**
and the gear's **Settings** (start with Windows, language, formats, accounts,
Karpo Pro, back up and reset, about). Starting `Karpo.exe` again while it runs
opens the gear's Settings.

### Build (to work on it)

```powershell
dotnet build Karpo.slnx -c Release
dotnet test Karpo.Tests
```

- The bar: `Karpo.Dock\bin\Release\...\Karpo.exe`
- `KARPO_UI_TESTS=1` adds the tests that draw real windows (on hidden desktops).
  Your own `dwm.exe` serves them, the one that draws your screen, so they make it
  stutter. One such run goes at a time on the PC, whatever the checkout or
  worktree, on four threads (`Karpo.Tests\UiTurn.cs`, `ui.runsettings`). A run
  that waits for its turn says so only at `--logger "console;verbosity=detailed"`;
  `%TEMP%\karpo-tests-ui-run.lock` names the run that has it.

### Checks

- `Karpo.exe --smoke` draws a bar with every kind of island and opens every
  flyout on a desktop of its own, where nobody sees it, then prints how long
  that took and the memory it holds. It doesn't touch a running Karpo, the
  taskbar or your settings (it works in a temporary folder). Add
  `--rounds 1000` to repeat what the bar does all day (rebuilding, flyouts,
  reading notifications) and watch the memory stay level.
- `./scripts/measure.ps1 -Name Karpo -Seconds 60` shows what a running Karpo
  costs: memory, CPU time and how often it wakes up. An idle bar should hardly
  wake at all.

## Where things live

| What | Where |
| --- | --- |
| Settings (readable JSON; edits apply as soon as the file is saved) | `%APPDATA%\Karpo\config.json` |
| Caches, the taskbar state to restore | `%LOCALAPPDATA%\Karpo\` |

Settings of earlier versions are not carried over: a `config.json` in an older
shape is kept aside as `config.shape<N>.json` and Karpo starts on its defaults
(nothing is read from `%APPDATA%\PierNet` or `%APPDATA%\pier` any more).

To start over, the edit bar's Settings has **Back up and reset**: *Reset
layouts* makes the bars, folders and widgets (and which layout each display
shows) what a new install has and keeps every other setting; *Reset all
settings* makes all of `config.json` a new install's, but for "Start with
Windows". Either keeps the file it replaces as
`config.json.before-reset-<date>` next to it, and leaves the accounts, their
sign-ins and the licence in `%LOCALAPPDATA%\Karpo` alone. From a script, the
bar's `Karpo.exe --reset-settings layouts` or `--reset-settings all` does the
same; a running bar takes the new file as it takes any saved edit.

## Layout of the repository

| Folder | Contents |
| --- | --- |
| `Karpo.Core` | The settings model, the menus' rows, the words, and display enumeration. |
| `Karpo.Dock` | The bar: windows, layout and scene, apps, tray, islands, flyouts, the edit bar and its menus. |
| `Karpo.Accounts` | The accounts: Microsoft, IMAP and CalDAV/CardDAV sign-ins and sync. |
| `assets/logo` | The logo in PNG and ICO. |
| `Karpo.Tests` | Tests of the settings model and the dock's logic. |
| `scripts` | `publish.ps1` for a build to use, `measure.ps1` for CPU and memory use. |

## Things worth knowing

Karpo leans on a few undocumented Windows interfaces, as other taskbar
replacements do (RetroBar's ManagedShell among them): it receives tray icons
through a hidden `Shell_TrayWnd`, resolves app identities with
`IApplicationResolver`, reads jump lists from their files, and reads the
virtual desktop list from the registry. A Windows update can change any of
these.

Notifications are read, never changed, from Windows' own notification store,
because the documented listener needs a packaged app. Dismissing one in Karpo
hides it only in Karpo.
