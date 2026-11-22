// No English left in a translation: a text another language writes exactly as English does is
// English that slipped through, unless it is a name (Karpo, Wi-Fi, Windows Update), a unit
// (GB, GHz, min), an address, nothing but places and signs ("{first}, {next}"), or a word that
// language's Windows writes the same (German's "System", French's "Notifications"), which is
// listed below by key, language by language, after somebody checked it. A new translation lands
// with every such text either translated or listed here.

using System.Text.RegularExpressions;

namespace Karpo.Tests.Localization;

public sealed partial class UntranslatedTests
{
    /// <summary>Whole texts that are names, the same in every language.</summary>
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "Karpo", "Karpo Pro", "Microsoft Store", "Windows", "Windows PowerShell", "Windows Update",
        "Snipping Tool", "Wi-Fi", "Bluetooth", "VPN", "OK", "CPU", "GPU", "Ethernet", "GitHub",
        "Celsius (°C)", "Fahrenheit (°F)",
        // camera: the streams' protocols, by the names every language writes them with (§331).
        "MJPEG", "HLS", "MPEG-DASH", "RTSP",
        // The keys' caps, as most languages' Windows writes them in a menu's shortcuts.
        "Ctrl", "Del", "Esc", "Shift",
    };

    /// <summary>Units and symbols, the same in every language that keeps them: sizes, frequencies, and the SI's for time and the screen's pixels.</summary>
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    {
        "B", "KB", "MB", "GB", "TB", "PB", "EB", "GHz", "MHz", "Hz", "ms", "s", "min", "h", "d", "px",
        // air: the air quality index's abbreviation, which MSN Weather writes so in every language (§210).
        "AQI",
        // quakes: kilometres and the compass's points, as most languages write them (§240).
        "km", "N", "NE", "E", "SE", "S", "SW", "W", "NW",
    };

    /// <summary>
    /// The texts each language writes as English does, on purpose: the word its Windows 11 uses, a
    /// loanword that language uses, a short label (a stock chart's "1M") as its finance sites write
    /// it. Checked by hand; a text here is not English left over.
    /// </summary>
    private static readonly Dictionary<string, string[]> Same = new(StringComparer.Ordinal)
    {
        ["de"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "core.preset.startGroup.title", "dock.add.beta", "core.startPlace.downloads", "core.startPlace.videos",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.apps", "core.winSearch.category.videos", "core.winSearch.category.web", "core.winSearch.app",
            // §517: the look editor's "Text" heading, the German word too.
            "settings.sections.text",
            // §613: the Appearance's Mode "System", as de's Windows writes the mode that follows it.
            "settings.appearance.system",
            // §514: the Look › section of what the widgets open, "Flyouts", as de's Windows and its Flyout outline write it.
            "settings.look.flyouts",
            // §577: the Appearance's Palette, the German word too.
            "settings.look.palette",
            // §498: a folder's details and views, as File Explorer writes them in de's Windows.
            "dock.folder.details.name", "settings.pin.view.details",
            // §489: an animation speed's "Normal", the word Windows writes too.
            "settings.general.animationSpeed.normal",
            // §425: a pin's kinds of target, as de's Windows writes them.
            "settings.pin.kind.app", "settings.pin.kind.website",
            "settings.widget.spatialFormat.header",
            // The sports' Basketball and Baseball, German words too (§369).
            "core.sports.basketball", "core.sports.baseball",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda", "core.clockStyle.analog", "core.widgetStyle.performanceDetails.details",
            // Windows 11's own German words: System, Sound, Apps, Widgets, Desktop, Start, Multitasking, Terminal.
            "core.barMenu.layouts", "dock.edit.layout", "core.barMenu.terminal", "core.barMenu.system", "core.barMenu.sound",
            "core.face.sound.title", "core.face.timer.title", "core.widget.widgetsBoard.title", "core.widget.apps.title",
            "core.widget.soundSwitch.title", "core.widget.timer.title", "core.action.sound", "core.action.multitasking",
            "core.actionSection.system", "core.actionSection.apps", "core.category.system", "core.preset.soundGroup.title",
            "core.preset.timerGroup.title", "core.preset.systemGroup.title",
            "core.market.label.month", "core.market.label.sixMonths", "core.market.label.yearToDate", "core.market.label.max",
            "dock.menu.widget", "dock.quickLink.system", "dock.quickLink.terminal", "dock.quickLink.desktop", "dock.uia.start",
            "dock.quick.sound.title", "dock.clock.timer", "dock.mail.title",
            "settings.license.usage.layouts",
            "settings.accounts.serverHeader", "settings.about.version", "settings.online.section",
            "settings.displays.edge.header",
            "settings.widget.market.symbol.header", "settings.widget.countdown.name.header",
            "settings.widget.notebook.name", "settings.widget.items.size.standard", "settings.widget.colorFilter.header",
            "settings.pin.name.header", "settings.pin.navigation.header", "settings.layouts.header",
            "settings.layouts.name.header", "settings.layouts.type.dock",
            "widget.part.start", "widget.performance.adapter", "widget.performance.threads", "widget.performance.handles",
            "widget.switch.hotspotShort", "settings.iconPicker.icon.video", "settings.iconPicker.icon.bus", "settings.iconPicker.icon.system",
            // German's "in 4 Min." before a meeting, as English's.
            "dock.meeting.in",
            // The air quality's "Index" and "Pollen", German words too (§210), the pollen's widget too (§590).
            "core.face.airQuality.index.title", "dock.air.pollen", "core.widget.airPollen.title",
            // German's colour names Orange, Gold and Pink.
            "settings.accounts.color.orange", "settings.accounts.color.gold", "settings.accounts.color.pink",
            // A match on now, "Live" as German's sports pages write it (§485).
            "dock.sports.live", "dock.sports.tip.live",
        ],
        ["fr"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam", "core.accounts.gmail.important",
            // §652: Windows search's groups of Karpo's own, the words this language's Windows and Karpo write too.
            "core.winSearch.category.people", "core.winSearch.category.notes",
            // §641: Start's words as this language's Windows writes them too.
            "core.startPlace.documents",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.documents", "core.winSearch.category.photos", "core.winSearch.category.web",
            // §603: a match's "Score", the French word too.
            "core.widget.sports.title",
            // §577: the Appearance's Palette, the French word too.
            "settings.look.palette",
            // §613: the Appearance's "Mode", the French word too ("Choisir votre mode").
            "settings.appearance.mode",
            // §563: a folder's page's "Faces" heading, the French word too (its faces' search "Rechercher des faces").
            "dock.panel.section.faces",
            // §517: ⚙'s "Services" heading, the French word too.
            "settings.sections.services",
            // §532: a taskbar menu shortcut's "Arguments", the French word too.
            "settings.barMenu.arguments",
            // §498: a folder's details and views, as File Explorer writes them in fr's Windows.
            "dock.folder.details.type",
            // §489: Karpo's "Animations", the word Windows writes too.
            "settings.general.animations.header",
            // sports (§479): a table's points, "Pts" and "Points", as French sites write them.
            "dock.sports.column.points", "dock.sports.column.pointsName",
            // windowsMenus: Windows' notifications button (§466), "Notifications" in fr's Windows.
            "core.widget.windowsNotifications.title",
            "settings.widget.volume.direction.header", "settings.widget.spatialFormat.header",
            // §462: a paper's "Grain", the French word too.
            "settings.appearance.styles.patternGrain",
            // sun (§368): civil twilight is "civil", an arc an "Arc", a place's "Latitude, longitude".
            "core.widgetStyle.sun.arc", "dock.sun.civil", "settings.widget.weather.place.coordinates",
            // The sports' Football and Baseball, French words too (§369).
            "core.sports.soccer", "core.sports.baseball",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda", "settings.widget.style.header",
            // The Clock app's lap table: Windows says Total here too.
            "dock.stopwatch.total",
            // The menu's time steppers (§224): French's "Minutes", as its time picker says.
            "dock.edit.minutes",
            "core.barMenu.terminal", "core.face.notifications.title", "core.face.contacts.title", "core.face.calendar.form.title",
            "core.widget.widgetsBoard.title", "core.widget.contacts.title", "core.widget.note.title", "core.widget.soundVolume.title", "settings.tray.volume",
            "core.widget.microphoneSwitch.title", "core.widget.actions.title", "core.action.notifications",
            "core.preset.notificationsGroup.title", "core.preset.contactsGroup.title", "core.preset.notesGroup.title",
            "core.market.choice.max", "core.market.label.month", "core.market.label.sixMonths", "core.market.label.yearToDate",
            "core.market.label.max", "core.accounts.defaultBook",
            "dock.widgetMenu.pause", "dock.menu.widget", "dock.quickLink.terminal", "dock.uia.menu", "dock.quick.sound.volume",
            "dock.quick.sound.microphone", "dock.clock.pause",
            "settings.localization.formats.section",
            "settings.accounts.gives.contacts", "settings.about.version",
            "settings.displays.edge.header",
            "settings.widget.note.note.header", "settings.widget.items.size.compact",
            "settings.widget.items.size.standard", "settings.pin.navigation.header", "settings.layouts.type.header",
            "settings.layouts.type.dock",
            "widget.media.pause", "widget.notifications.name", "widget.performance.threads", "widget.performance.handles",
            "widget.note.newNote", "widget.note.newNoteNumbered", "widget.note.format.code", "widget.sound.volume",
            "widget.switch.rotationShort", "settings.iconPicker.icon.document", "settings.iconPicker.icon.page",
            "settings.iconPicker.icon.bus", "settings.iconPicker.icon.train", "settings.iconPicker.icon.robot",
            "settings.iconPicker.icon.important", "widget.todo.important", "settings.iconPicker.icon.xbox", "settings.accounts.color.orange",
        ],
        ["es"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "dock.add.beta",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
            // §489: an animation speed's "Normal", the word Windows writes too.
            "settings.general.animationSpeed.normal",
            // sports (§483): a cup's Final, the word es's football writes.
            "dock.sports.round.final",
            // §485: ⚙ › General, as es's Windows names its page.
            "settings.general.header",
            // sun (§368): civil twilight is "civil".
            "dock.sun.civil",
            // §394: one error is "1 error" in Spanish too.
            "dock.edit.errors.one",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda",
            // The Clock app's lap table: Windows says Total here too.
            "dock.stopwatch.total",
            "core.barMenu.terminal", "core.widget.widgetsBoard.title",
            "core.market.label.day", "core.market.label.fiveDays", "core.market.label.month", "core.market.label.sixMonths",
            "core.market.label.yearToDate",
            "dock.menu.widget", "dock.quickLink.terminal", "dock.badge.error",
            "settings.layouts.type.dock",
            "widget.security.protections", "widget.switch.hotspotShort",
            "settings.iconPicker.icon.robot", "settings.accounts.color.header",
        ],
        ["zh-Hans"] = [],
        ["pt-BR"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "dock.add.beta", "dock.start.category.social", "core.startPlace.downloads",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.email", "core.winSearch.category.web",
            // §489: an animation speed's "Normal", the word Windows writes too.
            "settings.general.animationSpeed.normal",
            // sports (§483): a cup's Final, the word pt-BR's football writes.
            "dock.sports.round.final",
            // sports (§479): a table's points, "Pts" (pontos), as Brazilian sites write it.
            "dock.sports.column.points",
            // sun (§368): civil twilight is "civil", a place's "Latitude, longitude".
            "dock.sun.civil", "settings.widget.weather.place.coordinates",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda",
            // The Clock app's lap table: Windows says Total here too.
            "dock.stopwatch.total",
            // Brazilian Windows' own words and loanwords: Terminal (Admin), Widgets, Volume, Menu, Online, Mouse, Threads, Hotspot, Link.
            "core.barMenu.layouts", "dock.edit.layout", "core.barMenu.terminal", "core.barMenu.terminalAdmin",
            "core.barMenu.powerShellAdmin", "core.widget.widgetsBoard.title", "core.widget.soundVolume.title", "settings.tray.volume",
            "core.market.label.day", "core.market.label.fiveDays", "core.market.label.month", "core.market.label.sixMonths",
            "dock.menu.widget", "dock.quickLink.terminal", "dock.quickLink.admin", "dock.uia.menu",
            "dock.quick.sound.volume", "dock.clipboard.count.one", "dock.folder.items.one",
            "settings.license.usage.layouts", "settings.online.section",
            "settings.layouts.header",
            "settings.layouts.type.dock",
            "settings.iconPicker.icon.mouse", "widget.folder.widgets.one", "widget.folder.widgets.other",
            "widget.performance.threads", "widget.note.format.link", "widget.sound.volume", "widget.volume.level",
            "widget.switch.hotspotShort",
        ],
        ["it"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "core.preset.startGroup.title", "dock.add.beta", "dock.start.category.social",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web", "core.winSearch.app",
            // §498: a folder's details and views, as File Explorer writes them in it's Windows.
            "dock.folder.type.plain",
            // §425: a pin's kinds of target, as it's Windows writes them.
            "settings.pin.kind.app", "settings.pin.kind.file",
            // Baseball, an Italian word too (§369).
            "core.sports.baseball",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda",
            // An area chart is "grafico ad area" (§258).
            "settings.widget.market.chart.area",
            // The edit bar's Layout ▾ (§352): Italian Windows says Layout too.
            "dock.edit.layout",
            // Italian Windows' own words: Timer, Volume, Multitasking, Desktop, Start, Output, Input, Password, Server, Antivirus, Firewall.
            "core.face.timer.title", "core.widget.soundVolume.title", "settings.tray.volume", "core.widget.timer.title",
            "core.action.multitasking", "core.preset.timerGroup.title", "core.market.label.month",
            "core.market.label.sixMonths", "core.market.label.yearToDate", "core.market.label.max", "dock.menu.widget",
            "dock.quickLink.desktop", "dock.uia.start", "dock.uia.menu", "dock.quick.sound.volume",
            "dock.quick.sound.output", "dock.quick.sound.input", "dock.quick.wifi.password", "dock.clock.timer",
            "settings.accounts.passwordHeader", "settings.accounts.serverHeader",
            "settings.online.section",
            "settings.widget.items.size.standard", "settings.layouts.type.dock", "settings.iconPicker.icon.pc",
            "settings.iconPicker.icon.mouse", "settings.iconPicker.icon.robot", "widget.part.start",
            "widget.security.protections", "widget.sound.volume", "widget.switch.hotspotShort",
        ],
        ["zh-Hant"] =
        [
            // Taiwan writes the dock as Apple Taiwan does, Dock.
            "settings.layouts.type.dock",
        ],
        ["pl"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "core.preset.startGroup.title", "dock.add.beta",
            // §536: a look preview's stand-in event, "Lunch", a Polish word too.
            "dock.edit.previewLunch",
            // §425: a pin's kinds of target, as pl's Windows writes them.
            "settings.pin.kind.folder",
            "settings.widget.spatialFormat.header",
            // Baseball, a Polish word too (§369).
            "core.sports.baseball",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // Polish Windows' own words: System, Folder, Terminal, Start, Alarm, Separator, Symbol, Link, Hotspot, Narrator.
            "core.barMenu.terminal", "core.widget.narrator.title", "core.barMenu.system",
            "core.widget.folderList.title", "settings.folderContents.target.header", "dock.panel.section.folder", "core.widget.divider.title", "core.widget.alarm.title",
            "core.actionSection.system", "core.category.system", "core.catalogue.page.list",
            "core.preset.systemGroup.title", "core.market.label.day",
            "core.market.label.fiveDays", "core.market.label.month", "core.market.label.sixMonths",
            "core.market.label.yearToDate", "dock.quickLink.system", "dock.quickLink.terminal", "dock.uia.start",
            "dock.uia.menu", "dock.badge.alarm", "dock.badge.alert", "dock.clock.start", "dock.alarm.title",
            "settings.online.section", "settings.widget.market.symbol.header", "settings.iconPicker.icon.system",
            "settings.iconPicker.icon.robot", "widget.part.start", "widget.note.format.link",
            "widget.switch.hotspotShort",
        ],
        ["tr"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "dock.add.beta",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
            // §489: an animation speed's "Normal", the word Windows writes too.
            "settings.general.animationSpeed.normal",
            // sports (§483): a cup's Final, the word tr's football writes.
            "dock.sports.round.final",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetReading.performance.disk", "core.clockStyle.analog",
            // Turkish Windows' own words: Disk, Terminal, Alarm; a year is yıl, so 1Y and 5Y stay.
            "core.barMenu.terminal", "core.face.disk.title", "core.face.disk.disk.title", "core.widget.alarm.title",
            "core.addTree.disk.title", "core.preset.diskGroup.title", "core.market.label.year",
            "core.market.label.fiveYears", "core.market.label.years", "dock.quickLink.terminal", "dock.badge.alarm",
            "dock.alarm.title", "settings.widget.performance.disk.header", "settings.layouts.type.dock",
            "settings.iconPicker.icon.robot", "widget.performance.disk", "widget.rss.age.years",
        ],
        ["nl"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "core.preset.startGroup.title", "dock.start.category.entertainment", "dock.start.category.games", "core.startPlace.downloads",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.apps", "core.winSearch.category.web", "core.winSearch.app",
            // §603: a match's "Score", the Dutch word too.
            "core.widget.sports.title",
            // §536: a look preview's stand-in event, "Lunch", a Dutch word too.
            "dock.edit.previewLunch",
            // §498: a folder's details and views, as File Explorer writes them in nl's Windows.
            "dock.folder.details.type", "settings.pin.view.details",
            // §425: a pin's kinds of target, as nl's Windows writes them.
            "settings.pin.kind.app", "settings.pin.kind.website",
            // American football, as Dutch writes it (§369).
            "core.sports.football",
            // camera (§331): Dutch's own Camera and Protocol.
            "core.camera.url.title", "core.camera.protocol.title",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda", "core.widgetStyle.calendar.week", "core.widgetReading.performance.cpu", "core.widgetStyle.performanceDetails.details",
            // Dutch Windows' own words: Timer, Stopwatch, Processor, Apps, Focus, Recent, Accounts, Media, Label, Filter, Threads.
            "core.barMenu.terminal", "core.face.timer.title", "core.face.stopwatch.title", "core.face.cpu.title", "core.widget.widgetsBoard.title", "core.widget.apps.title",
            "core.widget.soundVolume.title", "settings.tray.volume", "core.widget.timer.title", "core.widget.stopwatch.title",
            "core.widget.alarm.title", "core.action.focus", "core.action.multitasking", "core.actionSection.apps",
            "core.addTree.cpu.title", "core.preset.timerGroup.title", "core.preset.stopwatchGroup.title",
            "core.preset.focusGroup.title", "core.preset.mediaGroup.title", "core.preset.cpuGroup.title",
            "core.market.choice.max", "core.market.label.day", "core.market.label.fiveDays", "core.market.label.month",
            "core.market.label.sixMonths", "core.market.label.yearToDate", "core.market.label.max", "dock.menu.widget",
            "dock.quickLink.terminal", "dock.uia.start", "dock.uia.menu", "dock.badge.alarm", "dock.jump.recent",
            "dock.quick.sound.volume", "dock.quick.wifi.open", "dock.clock.timer", "dock.clock.focus",
            "dock.stopwatch.title", "dock.alarm.title", "settings.nav.accounts",
            "settings.accounts.serverHeader", "settings.online.section",
            "settings.widget.items.size.compact", "dock.edit.size.compact", "settings.widget.colorFilter.header", "settings.layouts.type.header",
            "settings.layouts.type.dock", "settings.iconPicker.icon.document", "settings.iconPicker.icon.accounts",
            "settings.iconPicker.icon.stopwatch", "settings.iconPicker.icon.bus", "settings.iconPicker.icon.tag",
            "settings.iconPicker.icon.robot", "widget.part.start", "widget.media.media", "widget.performance.processor",
            "widget.performance.processorShare", "widget.performance.adapter", "widget.performance.threads",
            "widget.note.format.code", "widget.security.protections", "widget.sound.volume",
            "widget.switch.hotspotShort",
            // A match on now, "Live" as Dutch sports pages write it (§485).
            "dock.sports.live", "dock.sports.tip.live",
        ],
        ["id"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.email", "core.winSearch.category.web",
            // §498: a folder's details and views, as File Explorer writes them in id's Windows.
            "dock.folder.type.plain",
            // §613: the Appearance's "Mode", as id's Windows writes it ("Pilih mode Anda").
            "settings.appearance.mode",
            // §489: an animation speed's "Normal", the word Windows writes too.
            "settings.general.animationSpeed.normal",
            // sports (§483): a cup's Final, the word id's football writes.
            "dock.sports.round.final",
            // §425: a pin's kinds of target, as id's Windows writes them.
            "settings.pin.kind.folder", "settings.pin.kind.file",
            // §459: a style's Font, the word Windows writes too.
            "settings.appearance.styles.font",
            "settings.widget.spatialFormat.header", "settings.pin.target.header",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda", "core.clockStyle.analog", "core.widgetReading.performance.disk",
            // An area chart is "grafik area" (§258).
            "settings.widget.market.chart.area",
            // The Clock app's lap table: Windows says Total here too.
            "dock.stopwatch.total",
            // Indonesian Windows' own words: File Explorer, Timer, Stopwatch, Drive, Disk, Folder, Desktop, Output, Input, Keyboard.
            "core.barMenu.fileExplorer", "core.barMenu.terminal", "core.barMenu.terminalAdmin",
            "core.barMenu.powerShellAdmin", "core.face.timer.title", "core.face.stopwatch.title",
            "core.face.drive.title", "core.face.disk.title", "core.face.disk.disk.title",
            "core.widget.drive.title", "core.widget.folderList.title", "settings.folderContents.target.header", "dock.panel.section.folder", "core.widget.soundVolume.title", "settings.tray.volume",
            "core.widget.timer.title", "core.widget.stopwatch.title", "core.widget.alarm.title",
            "core.addTree.disk.title", "core.catalogue.page.list", "core.preset.timerGroup.title",
            "core.preset.stopwatchGroup.title", "core.preset.mediaGroup.title", "core.preset.driveGroup.title",
            "core.preset.diskGroup.title", "core.drive.type.drive",
            "core.market.label.yearToDate", "dock.widgetMenu.editFolder", "dock.menu.widget", "dock.quickLink.terminal",
            "dock.quickLink.admin", "dock.quickLink.fileExplorer", "dock.quickLink.desktop", "dock.uia.menu",
            "dock.badge.alarm", "dock.quick.sound.volume", "dock.quick.sound.output", "dock.quick.sound.input",
            "dock.clock.timer", "dock.stopwatch.title", "dock.alarm.title",
            "settings.accounts.serverHeader",
            "settings.widget.performance.disk.header",
            "settings.widget.drive.header",
            "settings.widget.colorFilter.header", "settings.layouts.type.dock", "settings.iconPicker.icon.fileExplorer",
            "settings.iconPicker.icon.stopwatch", "settings.iconPicker.icon.bus", "settings.iconPicker.icon.tag",
            "settings.iconPicker.icon.robot", "widget.keyboard.keyboard", "widget.media.media",
            "widget.performance.disk", "widget.security.protections", "widget.sound.volume",
            "widget.switch.hotspotShort",
        ],
        ["vi"] =
        [
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.email", "core.winSearch.category.web",
            // camera (§331): Camera, as Vietnamese Windows writes it.
            "core.camera.url.title",
            // Vietnamese Windows' own words: File Explorer, Terminal, Menu, Video; the dock as macOS writes it.
            "core.barMenu.fileExplorer", "core.barMenu.terminal", "dock.quickLink.terminal",
            "dock.quickLink.fileExplorer", "dock.uia.menu",
            "settings.layouts.type.dock", "settings.iconPicker.icon.video", "settings.iconPicker.icon.fileExplorer",
        ],
        ["te"] = [],
        ["ta"] = [],
        ["fa"] = [],
        ["ms"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
            // §517: Taskbar ▾'s "Bar" heading, as Malay's Windows writes "Bar tugas".
            "settings.sections.bar",
            // §425: a pin's kinds of target, as ms's Windows writes them.
            "settings.pin.kind.folder",
            "settings.widget.spatialFormat.header",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetStyle.calendar.agenda", "core.clockStyle.analog",
            // Malay Windows' own words: Terminal, Folder, Desktop, Menu, Output, Input, Import, Pin, Media, Label, Standard; item, popular, video, robot are Malay too.
            "core.barMenu.terminal", "core.widget.folderList.title", "settings.folderContents.target.header", "dock.panel.section.folder",
            "core.addTree.popular.title", "core.catalogue.page.list", "core.preset.mediaGroup.title",
            "core.market.label.yearToDate", "dock.widgetMenu.editFolder", "dock.menu.widget",
            "dock.quickLink.terminal", "dock.quickLink.desktop", "dock.uia.menu", "dock.desktops.numbered",
            "dock.quick.sound.output", "dock.quick.sound.input", "dock.clipboard.count.one", "dock.folder.items.one",
            "settings.about.import.confirm", "settings.widget.items.size.standard",
            "settings.iconPicker.icon.video", "settings.iconPicker.icon.folder",
            "settings.iconPicker.icon.pin", "settings.iconPicker.icon.robot", "widget.folder.widgets.one",
            "widget.media.media",
        ],
        ["th"] =
        [
            // Thai writes the dock as Apple's Thai does, Dock.
            "settings.layouts.type.dock",
        ],
        ["uk"] = [],
        ["ro"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §652: Windows search's groups of Karpo's own, the words this language's Windows and Karpo write too.
            "core.winSearch.category.events",
            // §641: Start's words as this language's Windows writes them too.
            "core.preset.startGroup.title", "dock.add.beta", "dock.start.category.social",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
            // §517: the look editor's "Text" heading, the Romanian word too.
            "settings.sections.text",
            // §485: ⚙ › General, as ro's Windows names its page.
            "settings.general.header",
            // §425: a pin's kinds of target, as ro's Windows writes them.
            "settings.pin.kind.folder",
            // §459: a style's Font, the word Windows writes too.
            "settings.appearance.styles.font",
            "settings.widget.spatialFormat.header",
            // sun (§368): civil twilight is "civil", an arc an "Arc".
            "core.widgetStyle.sun.arc", "dock.sun.civil",
            // Baseball, a Romanian word too (§369).
            "core.sports.baseball",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // camera (§331): Romanian's own Protocol.
            "core.camera.protocol.title",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.styled.calendar.title",
            // The Clock app's lap table: Windows says Total here too.
            "dock.stopwatch.total",
            // Romanian Windows' own words and Romanian ones spelled as English: Terminal, Calendar, Folder, Clipboard,
            // Multitasking, Inbox, Desktop, Start, General, Server, Online, Material, Text, Standard, Document, Mouse.
            "core.barMenu.terminal", "core.face.calendar.title",
            "core.widget.calendar.title", "core.widget.folderList.title", "settings.folderContents.target.header", "dock.panel.section.folder", "core.widget.divider.title",
            "core.action.clipboard", "core.action.multitasking", "core.category.calendar",
            "core.catalogue.page.list", "core.preset.calendarGroup.title", "core.preset.inboxGroup.title",
            "core.market.label.yearToDate", "core.market.label.max",
            "core.accounts.unnamedCalendar", "core.accounts.inbox", "dock.menu.widget", "dock.quickLink.terminal",
            "dock.quickLink.desktop", "dock.uia.start", "dock.desktops.numbered",
            "settings.accounts.serverHeader",
            "settings.accounts.sync.people.one", "settings.online.section", "settings.widget.items.size.compact",
            "settings.widget.items.size.standard", "settings.layouts.type.dock",
            "settings.iconPicker.icon.calculator", "settings.iconPicker.icon.video", "settings.iconPicker.icon.font",
            "settings.iconPicker.icon.document", "settings.iconPicker.icon.folder", "settings.iconPicker.icon.calendar",
            "settings.iconPicker.icon.laptop", "settings.iconPicker.icon.mouse", "settings.iconPicker.icon.puzzle",
            "settings.iconPicker.icon.robot", "settings.iconPicker.icon.important", "widget.todo.important", "widget.part.start",
            "widget.folder.widgets.one", "widget.note.format.link", "widget.switch.hotspotShort",
        ],
        ["ja"] =
        [
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
        ],
        ["el"] =
        [
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
        ],
        ["cs"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them.
            "core.accounts.gmail.spam",
            // §641: Start's words as this language's Windows writes them too.
            "core.preset.startGroup.title", "dock.add.beta",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.web",
            // §517: the look editor's "Text" heading, the Czech word too.
            "settings.sections.text",
            // Baseball, a Czech word too (§369).
            "core.sports.baseball",
            // A mail server's Port (§338), the word Windows writes too.
            "dock.accounts.port",
            // The widgets' styles (§209), each the word of the widget it was: Agenda, Volume, Disk, Week.
            "core.widgetReading.performance.disk",
            // Czech Windows' own words: Disk, Multitasking, Widget, Start, Server, Online, Symbol, Text, Video, Hotspot.
            "core.face.disk.title", "core.face.disk.disk.title", "core.action.multitasking", "core.addTree.disk.title",
            "core.preset.diskGroup.title", "core.market.label.day", "core.market.label.fiveDays", "core.market.label.month",
            "core.market.label.sixMonths", "core.market.label.yearToDate", "dock.menu.widget", "dock.uia.start",
            "settings.accounts.serverHeader", "settings.online.section",
            "settings.widget.market.symbol.header", "settings.widget.performance.disk.header", "settings.iconPicker.icon.video",
            "settings.iconPicker.icon.robot", "widget.part.start", "widget.folder.widgets.one", "widget.performance.disk",
            "widget.switch.hotspotShort",
        ],
        ["fil"] =
        [
            // §687: Gmail's own labels as this language's Gmail writes them, and a Google account as it writes a Microsoft one.
            "core.accounts.gmail.spam", "settings.accounts.google.header",
            // §652: Windows search's groups of Karpo's own, the words this language's Windows and Karpo write too.
            "core.winSearch.category.notes",
            // §641: Start's words as this language's Windows writes them too.
            "dock.start.category.social", "core.startPlace.network",
            // §642: Windows search's groups, as this language's Windows writes them.
            "core.winSearch.category.email", "core.winSearch.category.web", "core.winSearch.app",
            // Filipino Windows and its readers keep the English word for most computer terms (Taglish):
            // settings, apps, devices, units, and the tools Windows names in English.
            "settings.appearance.mode",
            "settings.appearance.system",
            "core.accounts.inbox",
            "core.accounts.kind.imap",
            "core.action.accessibility",
            "core.action.airplaneMode",
            "core.action.clipboard",
            "core.action.display",
            "core.action.energySaver",
            "core.action.focus",
            "core.action.mobileHotspot",
            "core.action.multitasking",
            "core.action.nearbySharing",
            "core.action.nightLight",
            "core.action.storage",
            "core.action.taskManager",
            "core.action.taskView",
            "core.action.taskbar",
            "core.action.volumeMixer",
            "core.actionSection.accessibility",
            "core.actionSection.system",
            "core.addTree.accessibility.title",
            "core.addTree.cpu.title",
            "core.addTree.disk.title",
            "core.addTree.gpu.title",
            "core.addTree.memory.title",
            "core.addTree.onOff",
            "core.barMenu.computerManagement",
            "core.barMenu.deviceManager",
            "core.barMenu.diskManagement",
            "core.barMenu.display",
            "core.barMenu.eventViewer",
            "core.barMenu.fileExplorer",
            "core.barMenu.powerShellAdmin",
            "core.barMenu.system",
            "core.barMenu.taskManager",
            "core.barMenu.terminal",
            "core.barMenu.terminalAdmin",
            "core.camera.protocol.title",
            "core.camera.url.title",
            "core.catalogue.page.list",
            "core.catalogue.page.taskbar",
            "core.category.display",
            "core.category.network",
            "core.category.performance",
            "core.category.system",
            "core.category.taskbar",
            "core.clockStyle.analog",
            "core.drive.label.cd",
            "core.drive.label.local",
            "core.drive.label.network",
            "core.drive.label.usb",
            "core.drive.type.drive",
            "core.drive.type.fixed",
            "core.drive.type.network",
            "core.drive.type.optical",
            "core.drive.type.ramDisk",
            "core.drive.type.removable",
            "core.face.airQuality.index.european",
            "core.face.airQuality.index.title",
            "core.face.airQuality.index.us",
            "core.face.calendar.zone.title",
            "core.face.clock.zone.title",
            "core.face.cpu.title",
            "core.face.dateTime.zone.title",
            "core.face.disk.disk.title",
            "core.face.disk.title",
            "core.face.drive.title",
            "core.face.gpu.adapter.title",
            "core.face.gpu.title",
            "core.face.hotspot.title",
            "core.face.icon.title",
            "core.face.mail.title",
            "core.face.memory.title",
            "core.face.nearbySharing.title",
            "core.face.networkDrive.title",
            "core.face.power.title",
            "core.face.stopwatch.title",
            "core.face.timer.title",
            "core.layout.defaultName",
            "core.market.label.day",
            "core.market.label.fiveDays",
            "core.market.label.fiveYears",
            "core.market.label.month",
            "core.market.label.sixMonths",
            "core.market.label.year",
            "core.market.label.yearToDate",
            "core.market.label.years",
            "core.market.share",
            "core.monitor.display",
            "core.preset.cpuGroup.title",
            "core.preset.diskGroup.title",
            "core.preset.driveGroup.title",
            "core.preset.focusGroup.title",
            "core.preset.gpuGroup.title",
            "core.preset.hotspotGroup.title",
            "core.preset.inboxGroup.title",
            "core.preset.mailGroup.title",
            "core.preset.mediaCenterGroup.title",
            "core.preset.mediaGroup.title",
            "core.preset.memoryGroup.title",
            "core.preset.nearbyGroup.title",
            "core.preset.networkDriveGroup.title",
            "core.preset.notesGroup.title",
            "core.preset.powerGroup.title",
            "core.preset.screenTimeGroup.title",
            "core.preset.stopwatchGroup.title",
            "core.preset.storageGroup.title",
            "core.preset.systemGroup.title",
            "core.preset.timerGroup.title",
            "core.preset.todoGroup.title",
            "core.preset.worldClockGroup.title",
            "core.sports.baseball",
            "core.sports.basketball",
            "core.sports.football",
            "core.sports.hockey",
            "core.sports.soccer",
            "core.styled.performance.title",
            "core.webSearch.custom",
            "core.widget.accessibility.title",
            "core.widget.airplaneMode.title",
            "core.widget.alarm.title",
            "core.widget.divider.title",
            "core.widget.drive.title",
            "core.widget.energySaver.title",
            "core.widget.folderList.title",
            "core.widget.gameMode.title",
            "core.widget.heading.title",
            "core.widget.liveCaptions.title",
            "core.widget.magnifier.title",
            "core.widget.mail.title",
            "core.widget.mobileHotspot.title",
            "core.widget.monoAudio.title",
            "core.widget.narrator.title",
            "core.widget.nearbyShare.title",
            "core.widget.networkDrive.title",
            "core.widget.nightLight.title",
            "core.widget.note.title",
            "core.widget.penMenu.title",
            "core.widget.pin.title",
            "core.widget.powerMode.title",
            "core.widget.projection.title",
            "core.widget.rotationLock.title",
            "core.widget.rss.title",
            "core.widget.soundInput.title",
            "core.widget.soundMixer.title",
            "core.widget.soundOutput.title",
            "core.widget.soundVolume.title",
            "core.widget.spatialSound.title",
            "core.widget.stickyKeys.title",
            "core.widget.stopwatch.title",
            "core.widget.taskView.title",
            "core.widget.timer.title",
            "core.widget.todo.title",
            "core.widget.touchKeyboard.title",
            "core.widget.virtualTouchpad.title",
            "core.widget.wifi.title",
            "core.widgetReading.performance.cpu",
            "core.widgetReading.performance.disk",
            "core.widgetReading.performance.gpu",
            "core.widgetReading.performance.memory",
            "core.widgetStyle.calendar.agenda",
            "core.widgetStyle.dailyForecast.graph",
            "core.widgetStyle.hourlyForecast.graph",
            "core.widgetStyle.performance.graph",
            "dock.accounts.port",
            "dock.air.pollen",
            "dock.air.pollen.alder",
            "dock.air.pollen.birch",
            "dock.air.pollen.mugwort",
            "dock.air.pollen.ragweed",
            "dock.alarm.title",
            "dock.badge.alarm",
            "dock.badge.available",
            "dock.badge.error",
            "dock.clipboard.count.one",
            "dock.clock.focus",
            "dock.clock.timer",
            "dock.desktops.numbered",
            "dock.edit.colorHex",
            "dock.edit.layout",
            "dock.edit.taskbar",
            "dock.folder.items.one",
            "dock.folder.type.plain",
            "dock.mail.title",
            "dock.menu.taskView",
            "dock.menu.widget",
            "dock.notifications.count.one",
            "dock.notifications.minutesAgo",
            "dock.panel.section.folder",
            "dock.quick.airplane.name",
            "dock.quick.cast.available",
            "dock.quick.energySaver",
            "dock.quick.hotspot.name",
            "dock.quick.nearby.title",
            "dock.quick.nightLight.name",
            "dock.quick.power.mode",
            "dock.quick.rotation.name",
            "dock.quick.sound.input",
            "dock.quick.sound.inputDevice",
            "dock.quick.sound.mixer",
            "dock.quick.sound.output",
            "dock.quick.sound.outputDevice",
            "dock.quick.sound.volume",
            "dock.quick.spatial.name",
            "dock.quick.vpn.error",
            "dock.quick.wifi.password",
            "dock.quick.wifi.secured",
            "dock.quick.wifi.user",
            "dock.quick.wired.address",
            "dock.quick.wired.domain",
            "dock.quickLink.admin",
            "dock.quickLink.computerManagement",
            "dock.quickLink.desktop",
            "dock.quickLink.deviceManager",
            "dock.quickLink.diskManagement",
            "dock.quickLink.eventViewer",
            "dock.quickLink.fileExplorer",
            "dock.quickLink.mobilityCenter",
            "dock.quickLink.system",
            "dock.quickLink.taskManager",
            "dock.quickLink.terminal",
            "dock.sports.column.difference",
            "dock.sports.column.points",
            "dock.sports.final",
            "dock.sports.halftime",
            "dock.sports.knockout",
            "dock.sports.live",
            "dock.status.cellular",
            "dock.status.network",
            "dock.stopwatch.lap",
            "dock.stopwatch.lapNumber",
            "dock.stopwatch.title",
            "dock.sun.civil",
            "dock.sun.polarDay",
            "dock.sun.polarNight",
            "dock.tides.high",
            "dock.tides.highAt",
            "dock.tides.low",
            "dock.tides.lowAt",
            "dock.trayIconShort",
            "dock.uia.menu",
            "dock.uia.start",
            "dock.uia.taskbar",
            "dock.uia.taskbarOn",
            "dock.widgetMenu.resourceMonitor",
            "settings.accounts.color.cornflower",
            "settings.accounts.color.lilac",
            "settings.accounts.color.teal",
            "settings.accounts.emailHeader",
            "settings.accounts.gives.mail",
            "settings.accounts.microsoft.header",
            "settings.accounts.passwordHeader",
            "settings.accounts.serverHeader",
            "settings.accounts.sync.events.one",
            "settings.accounts.sync.people.one",
            "settings.accounts.userHeader",
            "settings.appearance.styles.font",
            "settings.appearance.styles.patternGrid",
            "settings.appearance.theme.graphite",
            "settings.barMenu.shortcut",
            "settings.folder.icon.header",
            "settings.folderContents.target.header",
            "settings.iconPicker.icon.areaChart",
            "settings.iconPicker.icon.bus",
            "settings.iconPicker.icon.calculator",
            "settings.iconPicker.icon.click",
            "settings.iconPicker.icon.code",
            "settings.iconPicker.icon.commandPrompt",
            "settings.iconPicker.icon.diagnostic",
            "settings.iconPicker.icon.download",
            "settings.iconPicker.icon.ethernet",
            "settings.iconPicker.icon.fileExplorer",
            "settings.iconPicker.icon.fingerprint",
            "settings.iconPicker.icon.folder",
            "settings.iconPicker.icon.gameConsole",
            "settings.iconPicker.icon.headphones",
            "settings.iconPicker.icon.history",
            "settings.iconPicker.icon.keyboard",
            "settings.iconPicker.icon.laptop",
            "settings.iconPicker.icon.mail",
            "settings.iconPicker.icon.mobile",
            "settings.iconPicker.icon.monitor",
            "settings.iconPicker.icon.mouse",
            "settings.iconPicker.icon.network",
            "settings.iconPicker.icon.pc",
            "settings.iconPicker.icon.pieChart",
            "settings.iconPicker.icon.pin",
            "settings.iconPicker.icon.powerButton",
            "settings.iconPicker.icon.puzzle",
            "settings.iconPicker.icon.robot",
            "settings.iconPicker.icon.shoppingCart",
            "settings.iconPicker.icon.shuffle",
            "settings.iconPicker.icon.stopwatch",
            "settings.iconPicker.icon.sync",
            "settings.iconPicker.icon.system",
            "settings.iconPicker.icon.tag",
            "settings.iconPicker.icon.video",
            "settings.iconPicker.icon.wifi",
            "settings.iconPicker.icon.xbox",
            "settings.layouts.default.badge",
            "settings.layouts.icon.header",
            "settings.layouts.type.dock",
            "settings.layouts.type.taskbar",
            "settings.license.developer.title",
            "settings.online.section",
            "settings.pin.kind.app",
            "settings.pin.kind.file",
            "settings.pin.kind.folder",
            "settings.search.engine.header",
            "settings.statusIcons.airplaneMode",
            "settings.statusIcons.energySaver",
            "settings.statusIcons.hotspot",
            "settings.statusIcons.metered",
            "settings.tray.volume",
            "settings.widget.clock.zone.header",
            "settings.widget.colorFilter.grayscale",
            "settings.widget.colorFilter.header",
            "settings.widget.drive.header",
            "settings.widget.heading.text.placeholder",
            "settings.widget.items.size.compact",
            "settings.widget.market.chart.area",
            "settings.widget.market.chart.header",
            "settings.widget.market.found.crypto",
            "settings.widget.market.style.converter",
            "settings.widget.note.note.header",
            "settings.widget.performance.adapter.header",
            "settings.widget.performance.disk.header",
            "settings.widget.showDesktop.icon",
            "settings.widget.spatialFormat.header",
            "settings.widget.weather.place.coordinates",
            "widget.countdown.barMinutes",
            "widget.folder.widgets.one",
            "widget.keyboard.keyboard",
            "widget.markets.noSymbolCell",
            "widget.markets.search",
            "widget.media.media",
            "widget.media.shuffle",
            "widget.note.format.bold",
            "widget.note.format.code",
            "widget.note.format.italic",
            "widget.note.format.link",
            "widget.note.format.strike",
            "widget.note.newNote",
            "widget.note.newNoteNumbered",
            "widget.part.start",
            "widget.part.taskView",
            "widget.performance.activeTime",
            "widget.performance.adapter",
            "widget.performance.available",
            "widget.performance.baseSpeed",
            "widget.performance.cached",
            "widget.performance.committed",
            "widget.performance.disk",
            "widget.performance.graphics",
            "widget.performance.graphicsShare",
            "widget.performance.memory",
            "widget.performance.memoryShare",
            "widget.performance.network",
            "widget.performance.processor",
            "widget.performance.processorShare",
            "widget.performance.upTime",
            "widget.power.name",
            "widget.removable.count.one",
            "widget.rss.age.minutes",
            "widget.rss.age.weeks",
            "widget.rss.age.years",
            "widget.security.protections",
            "widget.sound.inputDevice",
            "widget.sound.outputDevice",
            "widget.sound.volume",
            "widget.sound.volumeMixer",
            "widget.switch.captionsShort",
            "widget.switch.hotspotShort",
            "widget.switch.nearbyShort",
            "widget.switch.rotationShort",
            "widget.switch.toggle",
            "widget.volume.level",
        ],
        ["my"] =
        [
            // Windows has no Burmese display language, so a Burmese reader meets these tools by their English
            // names: Task Manager, File Explorer, Run, Terminal, Device Manager and the rest, and Start.
            "core.action.taskManager",
            "core.action.taskView",
            "core.barMenu.computerManagement",
            "core.barMenu.deviceManager",
            "core.barMenu.diskManagement",
            "core.barMenu.eventViewer",
            "core.barMenu.fileExplorer",
            "core.barMenu.run",
            "core.barMenu.taskManager",
            "core.barMenu.terminal",
            "core.widget.taskView.title",
            "dock.menu.taskView",
            "dock.quickLink.computerManagement",
            "dock.quickLink.deviceManager",
            "dock.quickLink.diskManagement",
            "dock.quickLink.eventViewer",
            "dock.quickLink.fileExplorer",
            "dock.quickLink.mobilityCenter",
            "dock.quickLink.run",
            "dock.quickLink.taskManager",
            "dock.quickLink.terminal",
            "dock.uia.start",
            "dock.widgetMenu.resourceMonitor",
            "widget.part.start",
            "widget.part.taskView",
        ],
    };

    /// <summary>
    /// §631: the languages' names (CLDR's, for the Language search), which many languages write as English does — Hindi,
    /// Tamil, Hausa, Filipino are their names in German and Malay too — names, not English left over.
    /// </summary>
    private const string LanguageNames = "core.language.name.";

    public static TheoryData<string> Translations => [.. StringFiles.Translations];

    [Theory]
    [MemberData(nameof(Translations))]
    public void NoTextIsLeftInEnglish(string language)
    {
        var english = StringFiles.Map("en");
        var same = Same.TryGetValue(language, out var keys) ? keys.ToHashSet(StringComparer.Ordinal) : [];
        var left = StringFiles.Read(language)
            .Where(pair => pair.Value == english[pair.Key] && !same.Contains(pair.Key) && !Neutral(pair.Value) && !pair.Key.StartsWith(LanguageNames, StringComparison.Ordinal))
            .Select(pair => $"  \"{pair.Key}\": \"{pair.Value}\"")
            .ToList();
        Assert.True(
            left.Count == 0,
            $"{left.Count} texts in {language}.json are English's: translate them, or, where {language}'s Windows writes "
            + $"them the same, list their keys in UntranslatedTests.Same[\"{language}\"].\n{string.Join("\n", left)}");
    }

    [Fact]
    public void EveryListedKeyIsStillTheSameAsEnglish()
    {
        // A key listed for a text since translated, or gone, is listed no longer: the list stays the ones that are.
        var english = StringFiles.Map("en");
        foreach (var (language, keys) in Same)
        {
            if (!StringFiles.Codes.Contains(language))
            {
                continue;
            }
            var texts = StringFiles.Map(language);
            Assert.All(keys, key => Assert.True(
                texts.TryGetValue(key, out var text) && text == english[key] && !Neutral(text),
                $"UntranslatedTests.Same[\"{language}\"] lists \"{key}\", which is no longer English's text (or needs no listing)"));
        }
    }

    [Theory]
    [InlineData("{first}, {next}", true)]
    [InlineData("{date} · {time}", true)]
    [InlineData("5 GHz", true)]
    [InlineData("KB", true)]
    [InlineData("< 1 GB", true)]
    [InlineData("Wi-Fi", true)]
    [InlineData("Windows Update", true)]
    [InlineData("https://example.com/feed", true)]
    [InlineData("you@example.com", true)]
    [InlineData("Update", false)]
    [InlineData("Version {version}", false)]
    [InlineData("Open Windows Update", false)]
    [InlineData("{minutes:int} min", true)]
    [InlineData("Max", false)]
    public void NamesUnitsAndSignsAreTheSameEverywhere(string text, bool neutral) =>
        Assert.Equal(neutral, Neutral(text));

    /// <summary>A text every language may write as English does: a name, units, an address, or places and signs alone.</summary>
    private static bool Neutral(string text)
    {
        var trimmed = text.Trim();
        if (Names.Contains(trimmed) || IsAddress(trimmed))
        {
            return true;
        }
        var words = Word().Matches(Place().Replace(trimmed, " ")).Select(match => match.Value).ToList();
        return words.All(Units.Contains);
    }

    private static bool IsAddress(string text) =>
        !text.Contains(' ') && (text.Contains("://", StringComparison.Ordinal) || text.Contains('@') || text.EndsWith(".com", StringComparison.Ordinal));

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Place();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Word();
}
