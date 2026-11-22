// §641: the Start widgets' samples: a PC's apps, a week's files and an account, made up and built in, so a test, a
// picture and the add page's ghost show a Start full of things without reading the person's (§635's rule for the samples:
// a place that certainly has the data, no network, no account). The apps are Windows' own and the usual others, by their
// real Apps folder ids, so their icons are the PC's where it has them; the files are names only, nowhere on a disk.

using Karpo.Core;

namespace Karpo.Dock.StartWidgets;

/// <summary>§641: a made-up source (see the top of the file), at a fixed now so what it says never moves.</summary>
internal sealed class StartSamples : IStartSource
{
    private const string Apps = StartApp.AppsFolder;

    /// <summary>The account the samples are signed in as.</summary>
    internal static readonly StartAccount SampleAccount = new("Alex Morgan", null); // not-ui: a made-up person's name

    /// <summary>The apps, by name (as the Apps folder lists them).</summary>
    internal static readonly StartApp[] SampleApps =
    [
        new("Calculator", Apps + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
        new("Calendar", Apps + "microsoft.windowscommunicationsapps_8wekyb3d8bbwe!microsoft.windowslive.calendar"),
        new("Camera", Apps + "Microsoft.WindowsCamera_8wekyb3d8bbwe!App"),
        new("Clipchamp", Apps + "Clipchamp.Clipchamp_yxz26nhyzhsrt!App"),
        new("Clock", Apps + "Microsoft.WindowsAlarms_8wekyb3d8bbwe!App"),
        new("Copilot", Apps + "Microsoft.Copilot_8wekyb3d8bbwe!App"),
        new("Discord", Apps + "com.squirrel.Discord.Discord"),
        new("Excel", Apps + "Microsoft.Office.EXCEL.EXE.15"),
        new("File Explorer", Apps + "Microsoft.Windows.Explorer"), // not-ui: an app's name
        new("GIMP 2.10", Apps + "{6D809377-6AF0-444B-8957-A3773F02200E}\\GIMP 2\\bin\\gimp-2.10.exe"), // not-ui: an app's name
        new("Media Player", Apps + "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic"), // not-ui: an app's name
        new("Microsoft Edge", Apps + "MSEdge"), // not-ui: an app's name
        new("Microsoft Store", Apps + "Microsoft.WindowsStore_8wekyb3d8bbwe!App"), // not-ui: an app's name
        new("Microsoft To Do", Apps + "Microsoft.Todos_8wekyb3d8bbwe!App"), // not-ui: an app's name
        new("Minecraft Launcher", Apps + "Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft"), // not-ui: an app's name
        new("Notepad", Apps + "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
        new("OneNote", Apps + "Microsoft.Office.ONENOTE.EXE.15"),
        new("Outlook", Apps + "Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows"),
        new("Paint", Apps + "Microsoft.Paint_8wekyb3d8bbwe!App"),
        new("Phone Link", Apps + "Microsoft.YourPhone_8wekyb3d8bbwe!App"), // not-ui: an app's name
        new("Photos", Apps + "Microsoft.Windows.Photos_8wekyb3d8bbwe!App"),
        new("PowerPoint", Apps + "Microsoft.Office.POWERPNT.EXE.15"),
        new("Settings", Apps + "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel"),
        new("Snipping Tool", Apps + "Microsoft.ScreenSketch_8wekyb3d8bbwe!App"), // not-ui: an app's name
        new("Solitaire & Casual Games", Apps + "Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe!App"), // not-ui: an app's name
        new("Spotify", Apps + "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"),
        new("Steam", Apps + "Valve.Steam"),
        new("Task Manager", Apps + "{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\\Taskmgr.exe"), // not-ui: an app's name
        new("Terminal", Apps + "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"),
        new("Visual Studio Code", Apps + "Microsoft.VisualStudioCode"), // not-ui: an app's name
        new("WhatsApp", Apps + "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App"),
        new("Word", Apps + "Microsoft.Office.WINWORD.EXE.15"),
        new("Xbox", Apps + "Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App"),
        new("7-Zip File Manager", Apps + "{6D809377-6AF0-444B-8957-A3773F02200E}\\7-Zip\\7zFM.exe"), // not-ui: an app's name
    ];

    /// <summary>When the samples are: a Monday morning, so the week's files are each another day.</summary>
    internal static readonly DateTime Now = new(2026, 11, 9, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>The files opened lately: names only, nowhere on a disk.</summary>
    internal static readonly StartRecommendation[] SampleFiles =
    [
        new("Quarterly report.docx", @"C:\Users\Public\Documents\Quarterly report.docx", Now.AddMinutes(-12), false), // not-ui: a made-up file
        new("Budget 2027.xlsx", @"C:\Users\Public\Documents\Budget 2027.xlsx", Now.AddHours(-3), false), // not-ui: a made-up file
        new("Holiday photos.zip", @"C:\Users\Public\Downloads\Holiday photos.zip", Now.AddDays(-1), false), // not-ui: a made-up file
        new("Team offsite.pptx", @"C:\Users\Public\Documents\Team offsite.pptx", Now.AddDays(-2), false), // not-ui: a made-up file
        new("notes.md", @"C:\Users\Public\Documents\notes.md", Now.AddDays(-4), false),
        new("Invoice 1042.pdf", @"C:\Users\Public\Downloads\Invoice 1042.pdf", Now.AddDays(-9), false), // not-ui: a made-up file
    ];

    /// <summary>§655: the folders opened lately: names only, nowhere on a disk.</summary>
    internal static readonly StartRecommendation[] SampleFolders =
    [
        new("Projects", @"C:\Users\Public\Documents\Projects", Now.AddMinutes(-40), false), // not-ui: a made-up folder
        new("Downloads", @"C:\Users\Public\Downloads", Now.AddHours(-5), false), // not-ui: a made-up folder
        new("Holiday 2026", @"C:\Users\Public\Pictures\Holiday 2026", Now.AddDays(-2), false), // not-ui: a made-up folder
        new("Invoices", @"C:\Users\Public\Documents\Invoices", Now.AddDays(-6), false), // not-ui: a made-up folder
    ];

    private readonly Func<string, float, Action, Pixels?>? icon;

    /// <param name="icon">The icons, where a picture wants the PC's own; none for a test, which draws each app's glyph.</param>
    /// <param name="newApp">An app first seen a day before <see cref="Now"/> (Recently added); none for none.</param>
    internal StartSamples(Func<string, float, Action, Pixels?>? icon = null, string? newApp = "Clipchamp")
    {
        this.icon = icon;
        Uses = new StartUses();
        _ = Uses.Saw(SampleApps.Select(app => app.Target), Now.AddDays(-30));
        if (SampleApps.FirstOrDefault(app => app.Name == newApp) is { } added)
        {
            Uses.SeenAt(added.Target, Now.AddHours(-20));
        }
    }

    public int Version { get; private set; }

    IReadOnlyList<StartApp> IStartSource.Apps => SampleApps;

    public IReadOnlyList<StartRecommendation> Files => SampleFiles;

    public IReadOnlyList<StartRecommendation> Folders => SampleFolders;

    public StartUses Uses { get; private set; }

    public StartAccount Account { get; set; } = SampleAccount;

    public bool UpdateDue { get; set; } = true;

    public QuickLinkMachine Power { get; set; } = new(false, true, true, true);

    public event Action? Changed;

    /// <summary>What was opened, in order: what a test reads.</summary>
    internal List<string> OpenedTargets { get; } = [];

    public void Refresh()
    {
    }

    public void Opened(string target)
    {
        OpenedTargets.Add(target);
        Uses.Opened(target, Now);
        Version++;
        Changed?.Invoke();
    }

    public Pixels? Icon(string target, float px, Action came) => icon?.Invoke(target, px, came);
}
