# Karpo backlog

Collected 28–30 September 2026. Nothing here is started. Numbers stay stable, so an item can be
referred to by number; they follow the order the items were asked for, grouped here by theme.

## Bugs

- **15. Network Properties crashes Karpo.** Clicking Properties in the network flyout takes the
  whole app down. Likely the new code that opens Windows' own properties dialog for a saved network.
- **26. Crash when starting any app, on another laptop.** Seen on a different laptop. This PC also
  logged three crashes (fail-fast, 0xC0000409) on 28 Sep at 23:47–23:48, with dumps in
  `%LOCALAPPDATA%\CrashDumps`. Worth reading those dumps too.
- **12. Disk widget's read/write figures come and go.** They should always show, in a layout that
  doesn't shift as the numbers change.
- **16. Volume sound on every scroll notch.** Scrolling over the volume slider plays the tick once
  per notch; it should be debounced to one sound.
- **17. Energy saver icon doesn't update** after energy saver is turned on.
- **20. App previews stay open** after the pointer has moved away.
- **21. No hover highlight inside a subfolder.** Folders and pins inside a folder's subfolder don't
  highlight their background on hover.
- **22. Spotify's media buttons sometimes missing** (previous, play/pause, next).
- **27. Windows Security's "action recommended"** shows on Windows' taskbar but not on Karpo's.
- **33. Suggestion boxes reset the text** after every keystroke (to confirm which boxes: stock
  symbol, weather place, time zone, widget search).
- **35. Clicking away doesn't clear focus.** After clicking into a text field, clicking empty
  space doesn't take the focus or selection away; only another text field or navigating does.
  Most other controls behave the same, less visibly. (To confirm: Settings, Karpo's flyouts, or
  both.)

## Editing the bar and adding widgets

- **39. Add buttons on the bar.** An add button at the end of the taskbar, and add buttons to the
  left and right, where the spacers sit. **Reworked with the three sections** (2026-10-05: the bar
  goes back to three sections, left, middle and right, or top, middle and bottom; spacers removed
  completely): every section has its own add button at its end, adding into that section, and an
  empty section keeps its add button (and is a drop zone) in edit mode while taking no room at rest
  (docs\widgets.md §7b, §13).
- **40. No widget gallery any more.** Clicking add anywhere opens one list of widgets, grouped, each
  with its icon and name. One list, which may use several columns when needed. Since 2026-10-05 the
  list has no spacer: the bar's three sections replace it.
- **1. Settings, Remove and Duplicate only in edit mode.** These rows appear in a widget's
  right-click menu only while the taskbar is in edit mode.
- **3. Three widget libraries:** one for the taskbar, one for grid folders, one for list folders.
- **5. Edit button on folders.** An edit icon in the bottom-right corner of a folder that switches
  to edit mode. **Taken out again** (2026-10-05: "remove the edit button and close button from the
  folder views again"), with the close control an arranged folder had in the same place.
- **24. Make adding widgets easier.**
- **4. Improve editing the bar in general.** Parked: the person will try the add buttons, the
  widget list and the folder edit button first (39, 40, 5) and say what still bothers them.

## Pins and folders

- **2. Pins synced with Windows.** A pin added to Windows 11's taskbar is added to every open or
  visible Karpo layout, after its last pin. Removing a pin in Karpo removes it from Windows'
  taskbar too, but only once no layout holds it any more.
- **14. Folder widget for list and grid.** Shows a folder's contents inside the card or line,
  styled like the drive flyout's items. **Consolidated into the pin** (2026-10-04, with 28: "there
  should only be one way to pin/show a folder. the pin should be that way"): a pin to a folder set to
  show "Its contents" is this card and line, and on the taskbar a row of the folder's items, a
  button each, optionally with their titles. The separate widget is gone; a file that held one reads
  it as such a pin (docs\widgets.md §32).
- **28. Folder pin modes.** A pin to a folder gets one of three click modes:
  - open the folder in File Explorer;
  - one level: a flyout of its contents;
  - cascading: subfolders open submenus, with unlimited depth, each opening **on click**, not on
    hover.

  **Consolidated** (2026-10-04, with 14): a pin to a folder has two settings, the same on the
  taskbar, in grid folders and in list folders — how to show (just the folder, or its contents) and
  its navigation (these three modes), which applies to the pinned folder itself with "Just the
  folder" and to each subfolder shown with "Its contents"; files always just open. The lists have no
  folder title and no "Open in File Explorer" footer (a folder row's right click has it), and the
  cascade goes as deep as the folders do from every place. The grid's "always File Explorer" is gone
  (docs\widgets.md §32).
- **29. Heading for list view.** A title line to group the lines under it; one level only, list
  view only.

## New widgets

- **10. Snipping Tool shortcut** that starts a screenshot straight away, if Windows allows it.
- **13. Clipboard history.** Taskbar only; shows the recent clipboard values, images too if
  possible, in a flyout.
- **18. Settings shortcut** that opens Windows Settings.
- **23. Phone Link shortcut.** A widget that opens Phone Link. The app offers no way to read its
  data, so a shortcut only. **Removed at the person's request** (2026-10-04: "remove phone link
  widget completely from everywhere. it can just be replaced by a user by a pin"). A bar or
  folder that held it gets a pin to Phone Link in its place.

## Widget improvements

- **7. Now playing shows its source.** The flyout shows the playing app's icon, or where possible
  the favicon of the website it plays on.
- **11. Graphs across the whole widget.** Processor, memory, graphics and the other graphs, and
  stock, span the whole width and fade behind the text.
- **19. Bluetooth flyout shows paired devices only.** An "Add device" button opens Windows' own
  settings for adding a device.
- **31. Bluetooth battery as an icon.** A device's battery shows as a battery icon filled to its
  level (with the percentage beside it, as Windows does).
- **32. Stock chart range.** Each stock widget can be set how far back its chart goes, including
  "the last X years".
- **34. Search and Search Google in Windows 11's style.** Both get an option to show a search box
  or only the icon, and the box looks like Windows 11's.

## Search and Start

- **36. Search closes on a second click.** Clicking Search while Windows Search is open closes it
  instead of opening it again.
- **37. Karpo's flyouts close Windows Search.** Opening any Karpo flyout closes Windows Search (and
  Start) if it's open.
- **38. Start is Windows' Start.** Karpo's own Start menu was built, then removed at the person's
  request: the Start button, a lone press of the Windows key and Ctrl+Esc always open Windows' own
  Start, placed at Karpo's Start button (the first one where there are several).

## Defaults and screens

- **25. Better defaults.** Windows' taskbar hidden by default, and Karpo's never hidden.
- **30. Keyboard layout on the default bar,** as on Windows 11.
- **6. Same layout on every screen.** An option that switches the layout on every screen when it
  is switched on one.
- **41. The default layout is Windows'.** "you should make the default layout that shows with new
  installations as close as possible to how windows would show it": a new install's or a reset's bar
  is Windows 11's taskbar entry by entry, following Windows' own taskbar settings (docs\widgets.md
  §23.5).

## Later: to brainstorm, not to build yet

- **8. Workspaces.** Saved sets of apps, each with its screen, position and size, started with one
  click.
