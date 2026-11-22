// menus: every settings menu Karpo has, built by the code the dock builds it with, as rows (FlyoutItem)
// — for the menu rules (MenuRulesTests) to walk. Each widget kind's and each folder face's menu while the
// bar is arranged, every premade folder's, Display ▾, Layout ▾, the bar's and an arranged folder room's
// right click, ⚙ with its styles (each its editor, §467), accounts, holidays, licence, backup and About given, the taskbar's right click
// at rest, and the two add lists. Nothing is live; every list a row chooses from is listed, so every row shows.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

/// <summary>A menu of the corpus: its name, its rows, and whether it is a settings menu (a configuration menu, the rules' whole).</summary>
internal sealed record CorpusMenu(string Name, IReadOnlyList<FlyoutItem> Items, bool Settings = true, FlyoutLayout Layout = FlyoutLayout.List);

internal static class MenuCorpus
{
    /// <summary>To Do written to nowhere: the lists' rename and delete shown, never done.</summary>
    private sealed class NoLists : ITodoLists
    {
        public bool Rename(string account, string list, string name) => false;

        public bool Delete(string account, string list) => false;
    }

    public const string Display = "\\\\.\\DISPLAY1";

    /// <summary>Every list a row chooses from, listed.</summary>
    public static readonly OptionDevices Sources = new(["GPU 0: Intel UHD Graphics 770", "GPU 1: NVIDIA GeForce RTX 4060 Ti"], ["Disk 0 (C:)", "Disk 1 (D:)"])
    {
        Zones = ["W. Europe Standard Time", "Tokyo Standard Time"],
        Drives = [new DriveVolume(@"\\?\Volume{1}\", "C:", "Local Disk", DriveKind.Fixed, 100, 50)],
        TodoSources = [new TodoSource("me@example.com", "tasks", "Tasks", Default: true) { AccountName = "Ada Lovelace" }, new TodoSource("me@example.com", "shop", "Groceries") { AccountName = "Ada Lovelace" }],
        // A to-do line's lists as the dock lists them: renamed and deleted in place, under a heading per place (§445).
        TodoLists = new NoLists(),
        Notebook = new NotebookFile { Lists = [new TodoList { Id = "a", Name = "Home" }], Notes = [new Note { Id = "n1", Title = "Ideas" }] },
        // §455: what a calendar, a mail or a contacts widget may show from, two accounts' under their names.
        Parts =
        [
            new SourcePart("me@example.com|mail", "Inbox", "Ada Lovelace"),
            new SourcePart("me@example.com|mail:Archive", "Archive", "Ada Lovelace", Default: false),
            new SourcePart("me@example.com|calendar:home", "Calendar", "Ada Lovelace"),
            new SourcePart("ada@contoso.com|contacts:default", "Contacts", "Contoso"),
            new SourcePart("holidays|CH", "Switzerland", WidgetSources.HolidaysGroup),
        ],
        FirstDay = DayOfWeek.Monday,
        TrayIcons =
        [
            new TrayMenuIcon("onedrive", "Microsoft OneDrive", false, "OneDrive - Personal"),
            new TrayMenuIcon("steam", "Steam", true, "Steam"),
        ],
        Targets = new TargetChoices
        {
            Folders = [TargetChoices.Row("Documents", @"C:\Users\Ada\Documents", OptionGlyphs.Document)],
            Places = [TargetChoices.Row("This PC", "shell:MyComputerFolder", OptionGlyphs.Monitor)],
            Files = [TargetChoices.File("Report.docx", @"C:\Users\Ada\Documents\Report.docx")],
            Sites = [TargetChoices.Site("example.com", "https://example.com")],
            Apps = [TargetChoices.App("Calculator", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")],
        },
    };

    /// <summary>§592: the Appearance page's (<paramref name="items"/>, its menu) rows of <paramref name="part"/>: its tab's level.</summary>
    public static List<FlyoutItem> Part(IReadOnlyList<FlyoutItem> items, LookPart part) =>
        [.. items.Single(item => item.Text == LookParts.Name(part) && item.Children is not null).Children!];


    public static Config Fresh()
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        config.Holidays = new HolidayOptions();
        _ = OptionHolidays.AddCountry(config.Holidays, "CH");
        _ = BarLooks.Add(config, "Work");
        return config;
    }

    /// <summary>The second display of <see cref="TwoDisplays"/>.</summary>
    public const string Second = "\\\\.\\DISPLAY2";

    /// <summary>
    /// §579: <paramref name="config"/> on a PC of two displays — <see cref="Display"/> ("One", the bar the page is opened from)
    /// and <see cref="Second"/> ("Two", its bar on the top, Windows' primary: listed first) —, the bar of the first as the
    /// Display page is given it.
    /// </summary>
    public static BarSpot TwoDisplays(Config config)
    {
        config.EditScreen(Second, "Two").Edge = Edge.Top;
        return new BarSpot(Display, "One") { Attached = [Display, Second], Connected = [(Second, "Two"), (Display, "One")] };
    }

    /// <summary>§638: what the Microsoft account's parts hold, said at its head's end while it is shut.</summary>
    public static string MsHolds => S.SettingsAccountsSyncPair(first: S.SettingsAccountsSyncEvents(56), second: S.SettingsAccountsSyncMessages(1234));

    /// <summary>
    /// §535: what is synced of the Microsoft account: two calendars (one off, one coloured by the person), §646 its mail on its
    /// Mail folders page: its inbox, its archive (on) and its receipts (off).
    /// </summary>
    public static readonly IReadOnlyList<OptionAccountGroup> Parts =
    [
        new(S.SettingsAccountsGivesCalendars,
        [
            new OptionAccountPart("calendar:work", "Work", OptionGlyphs.Month, true) { Detail = "56 events", Automatic = "#038387" },
            new OptionAccountPart("calendar:home", "Home", OptionGlyphs.Month, false) { Detail = "Not synced", Colour = "#E3008C", Automatic = "#3A96DD" },
        ])
        {
            Glyph = OptionGlyphs.Month,
        },
        new(S.SettingsAccountsGivesMail,
        [
            new OptionAccountPart("inbox", "Inbox", OptionGlyphs.Mail, true) { Detail = "1234 messages", Automatic = "#8764B8" },
            new OptionAccountPart("mail:AR", "Archive", OptionGlyphs.Folder, true) { Detail = "512 messages · 1 unread" },
            new OptionAccountPart("mail:RE", "Receipts", OptionGlyphs.Folder, false) { Detail = "Not synced · 3 unread" },
        ])
        {
            Folders = S.SettingsAccountsMailFoldersInboxAnd(1),
            Glyph = OptionGlyphs.Mail,
        },
    ];

    /// <summary>⚙'s services, every section given: two accounts (the Microsoft one's parts, §535), the forms, the licence, the backup, About.</summary>
    public static KarpoServices Services()
    {
        var forms = new AccountForms((_, _) => Task.FromResult(new AccountTry("")), work => work(), () => { });
        var accounts = new OptionAccounts(
            [new OptionAccount("ms", "me@outlook.com", OptionAccountKind.Microsoft) { Groups = Parts, Holds = MsHolds }, new OptionAccount("imap", "me@example.com", OptionAccountKind.Mail)],
            _ => { },
            _ => { },
            _ => { },
            forms);
        return new KarpoServices(accounts, () => { })
        {
            Licence = new KarpoLicence(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree), true, true, () => { }, () => { }) { Price = "CHF 9.00" },
            Backup = new KarpoBackup(() => { }, () => { }, () => { }, _ => { }) { FilePath = @"C:\Users\Ada\AppData\Roaming\Karpo\config.json" },
            About = new KarpoAbout("1.4.2", ".NET 10.0.1"),
        };
    }

    /// <summary>
    /// A widget's menu while its bar is arranged, as the dock builds it (Dock.WidgetMenu): its buttons, its settings, a
    /// line's height, its look; §481 a folder's of <paramref name="side"/>: its face's (its right click: its faces,
    /// its Face style) or its panel's (Folder ▾ once it stands open).
    /// </summary>
    public static List<FlyoutItem> Arranged(WidgetItem item, OptionPlace place, FolderSide side = FolderSide.Face)
    {
        List<OptionPick> picks = [];
        return [.. TheDock.WidgetRowsFor(item), .. OptionMenu.Settings(item, picks, Sources, place, side, roomCells: item is CellItem ? LineHeight.Most : null)];
    }

    public static IEnumerable<CorpusMenu> All()
    {
        var config = Fresh();
        var place = new OptionPlace(config, Dark: true);
        var machine = new PresetMachine(HasBattery: true, FixedDrives: []);
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            var item = kind.New();
            if (item.Pin is { } pin)
            {
                pin.Target = @"C:\Users\Ada\Documents";
            }
            yield return new CorpusMenu($"widget {kind}", Arranged(item, place));
        }
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            yield return new CorpusMenu($"line {kind}", Arranged(kind.New(), place));
        }
        foreach (var face in FolderFaces.All)
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(face.Id)];
            yield return new CorpusMenu($"face {face.Id}", Arranged(folder, place));
        }
        foreach (var preset in Presets.All)
        {
            yield return new CorpusMenu($"folder {preset.Id}", Arranged(Presets.Folder(preset, machine), place));
            // §481: and its panel's, from Folder ▾ once it stands open.
            yield return new CorpusMenu($"folder {preset.Id} (opened)", Arranged(Presets.Folder(preset, machine), place, FolderSide.Panel));
        }
        // §585: the Layouts page of two displays, in Windows' order, this one marked.
        var bar = new BarSpot(Display, "One") { Attached = [Display], Connected = [(Second, "Two"), (Display, "One")] };
        foreach (var scope in Enum.GetValues<LooksScope>())
        {
            var name = scope switch
            {
                LooksScope.Layouts => "Layouts page",
                LooksScope.AddLayout => "Add a layout page",
                LooksScope.Taskbar => "Taskbar page",
                LooksScope.Appearance => "Appearance page",
                _ => "folder room right click",
            };
            // §628: Add a layout's presets made for a laptop as Windows ships it, never this PC.
            yield return new CorpusMenu(name, TheDock.LooksItems(config, bar, dark: true, [], scope: scope, machine: Karpo.Tests.Core.LayoutPresetsTests.Laptop));
        }
        yield return new CorpusMenu("Karpo page", TheDock.SettingsItems(config, Services(), []));
        yield return new CorpusMenu("Accounts page", TheDock.SettingsItems(config, Services(), [], page: KarpoPage.Accounts));
        var layouts = TheDock.LayoutRows([new TaskbarLayout { Id = "work", Name = "Work" }, new TaskbarLayout { Id = "home", Name = "Home" }], "work");
        yield return new CorpusMenu("bar right click (at rest)", TheDock.BarRows(layouts), Settings: false);
    }

    /// <summary>One line per row, indented by its level: what it says, its value, its control, what it is.</summary>
    public static string Dump(IReadOnlyList<FlyoutItem> items, int depth = 0)
    {
        var text = new System.Text.StringBuilder();
        foreach (var item in items)
        {
            var pad = new string(' ', depth * 2);
            if (item.Separator)
            {
                text.Append(pad).AppendLine("———");
                continue;
            }
            var icon = item.Icon is FlyoutIcon.Glyph glyph ? $"<{string.Join("", glyph.Code.Select(c => ((int)c).ToString("X4")))}>" : item.Icon is null ? "<>" : "<pic>";
            var value = item.Value is { } v ? $" = \"{v.Text}\"{(v.Icon is null ? "" : "+icon")}" : "";
            var control = item.Control switch
            {
                FlyoutControl.Toggle toggle => $" [switch {(toggle.On ? "on" : "off")}]",
                FlyoutControl.Radio radio => $" [radio {(radio.On ? "on" : "off")}]",
                FlyoutControl.Stepper step => $" [− {step.Value} +]",
                FlyoutControl.Segments seg => $" [segments {string.Join("|", seg.Buttons.Select(b => b.Name))} @{seg.Chosen}]",
                FlyoutControl.Tools tools => $" [tools {string.Join("|", tools.Buttons.Select(b => b.Grip ? "grip" : b.Name))}{(tools.Switch is { } on ? $" switch {(on ? "on" : "off")}" : "")}]",
                FlyoutField field => $" [field \"{field.Text}\" ph=\"{field.Placeholder}\"{(field.Finder is null ? "" : " finder")}{(field.Masked ? " masked" : "")}{(field.Filters ? " filters" : "")}]",
                null => "",
                var other => $" [{other.GetType().Name}]",
            };
            List<string> flags = [];
            if (item.Header)
            {
                flags.Add("HEADER");
            }
            if (!item.Enabled)
            {
                flags.Add("grey");
            }
            if (item.Checked is { } c)
            {
                flags.Add(c ? "ticked" : "untick");
            }
            if (item.PickOne)
            {
                flags.Add("pickone");
            }
            if (item.Stays)
            {
                flags.Add("stays");
            }
            if (item.Foot)
            {
                flags.Add("foot");
            }
            if (item.Note)
            {
                flags.Add("note");
            }
            if (item.Opens is not null)
            {
                flags.Add("link");
            }
            if (item.Primary)
            {
                flags.Add("primary");
            }
            if (item.Reorder != 0)
            {
                flags.Add($"reorder{item.Reorder}");
            }
            var detail = item.Detail is { Length: > 0 } d ? $" // {d}" : "";
            text.Append(pad).Append(icon).Append(' ').Append(item.Text).Append(value).Append(control).Append(flags.Count > 0 ? " {" + string.Join(",", flags) + "}" : "").Append(detail).AppendLine();
            if (item.Children is { Count: > 0 } children)
            {
                text.Append(Dump(children, depth + 1));
            }
        }
        return text.ToString();
    }
}
