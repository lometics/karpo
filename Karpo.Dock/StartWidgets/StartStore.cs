// §641: what the Start widgets show, read off Windows (docs\widgets.md §641), and the one way they are given it.
//
// Every Start line reads one source (IStartSource): the dock's is StartStore.Shared, which reads Windows off the dock's
// thread and says when it has (Changed); a test or a picture puts the samples in its place (StartSamples, Stand), so no
// test reads the PC's apps, files or account, launches anything or does a power command.
//
// What it reads, each by a documented way:
//  * the apps: the shell's Apps folder (FOLDERID_AppsFolder, shell:AppsFolder), Start's own All apps — every packaged
//    app and every shortcut of the Start menu's folders, each once, by its name (InstalledApps, the pins' picker's);
//  * the files: Windows' Recent folder (FOLDERID_Recent), a shortcut per file opened lately, newest first, each a file
//    on this PC's own disks that is still there (RecentFiles.Items, the pins' picker's Recent, with when); §655 the
//    folders opened lately the same way, Windows search's home's;
//  * the account: its display name (GetUserNameExW, NameDisplay; the user name where it has none) and its picture, the
//    one Windows shows on its sign-in screen and in Start (HKLM\…\AccountPicture\Users\<SID>, its files);
//  * whether a restart for updates is due (WindowsUpdateRegistry, the status icon's own read) and what Windows' power
//    menu offers (QuickLinkMachine, the power buttons' own read);
//  * Karpo's own record of what was opened from Start and when each app was first seen (StartUses, start.tsv).
//
// Nothing is read while no Start line shows: a line shown asks for what is stale (a minute, as the pins' picker's reads).
// §658: what it reads, how it runs off the dock's thread and its clock are handed in (StartReads): the dock's are Windows',
// a test's its own, so the store itself is tested without the PC's apps or files.

using System.Globalization;
using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;
using Microsoft.Win32;

namespace Karpo.Dock.StartWidgets;

/// <summary>§641: the account signed in, as Start's foot shows it: its name and its picture (null for none: its initials stand).</summary>
internal sealed record StartAccount(string Name, Pixels? Picture)
{
    /// <summary>What stands in a circle where there is no picture: the first letters of its first two words, in capitals.</summary>
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpper(word[0], CultureInfo.CurrentCulture)));
}

/// <summary>§641: what the Start widgets show (see the top of the file).</summary>
internal interface IStartSource
{
    /// <summary>Goes up whenever anything it gives changes, so a line knows to draw again.</summary>
    int Version { get; }

    /// <summary>Every app, by name; none until the first read is in.</summary>
    IReadOnlyList<StartApp> Apps { get; }

    /// <summary>The files opened lately, newest first.</summary>
    IReadOnlyList<StartRecommendation> Files { get; }

    /// <summary>§655: the folders opened lately, newest first (Windows search's home).</summary>
    IReadOnlyList<StartRecommendation> Folders { get; }

    /// <summary>What was opened from Start, and when each app was first seen.</summary>
    StartUses Uses { get; }

    /// <summary>The account signed in.</summary>
    StartAccount Account { get; }

    /// <summary>A restart for Windows' updates is due: the power menu offers to update first, its button wears a dot.</summary>
    bool UpdateDue { get; }

    /// <summary>What Windows' power menu offers: sleep, hibernate.</summary>
    QuickLinkMachine Power { get; }

    /// <summary>Said when anything has changed, from whatever thread read it.</summary>
    event Action? Changed;

    /// <summary>Reads again what is stale, off the caller's thread.</summary>
    void Refresh();

    /// <summary>An app or a file opened from a Start widget: counted, and written down.</summary>
    void Opened(string target);

    /// <summary>The icon of <paramref name="target"/> (an app, a file) drawn <paramref name="px"/> px across; null while it comes, <paramref name="came"/> called once it has.</summary>
    Pixels? Icon(string target, float px, Action came);
}

/// <summary>§641: the Start widgets' source: the dock's, else a test's (<see cref="Stand"/>).</summary>
internal static class StartSources
{
    [ThreadStatic]
    private static IStartSource? stand;

    /// <summary>tests: the source a line made on this thread reads; null for the dock's own.</summary>
    internal static IStartSource? Stand
    {
        get => stand;
        set => stand = value;
    }

    /// <summary>What a line made now reads.</summary>
    internal static IStartSource Now => Stand ?? StartStore.Shared;
}

/// <summary>§658: what <see cref="StartStore"/> reads, how it runs the reading off the caller's thread, and its clock in ms.</summary>
internal sealed record StartReads(
    Func<IReadOnlyList<InstalledApp>> Apps,
    Func<IReadOnlyList<RecentItem>> Recent,
    Func<StartAccount> Account,
    Func<bool> UpdateDue,
    Func<QuickLinkMachine> Power,
    Action<Action> Run,
    Func<long> Ticks);

/// <summary>§641: the dock's source: Windows, read off the dock's thread (see the top of the file).</summary>
internal sealed class StartStore : IStartSource
{
    /// <summary>How long a reading stands before a line shown asks again.</summary>
    private static readonly TimeSpan Kept = TimeSpan.FromMinutes(1);

    /// <summary>The most recent files read: the newest; Recommended lists twelve at most.</summary>
    private const int MostFiles = 24;

    /// <summary>The side the account's picture is decoded at: a 32 DIP circle at 300 %.</summary>
    private const int PictureSide = 96;

    private static StartStore? shared;

    /// <summary>The dock's.</summary>
    public static StartStore Shared => shared ??= new StartStore(new StartReads(
        InstalledApps.List,
        RecentFiles.Items,
        ReadAccount,
        static () => WindowsUpdateRegistry.Read().Restart,
        QuickLinkMachine.Read,
        static work => _ = Drives.DriveReader.Start(work),
        static () => Environment.TickCount64));

    private readonly StartReads reads;
    private readonly Lock gate = new();

    // §658: when it last read, by the clock in ms; null before the first. Until §658 long.MinValue, which the minute's test
    // overflowed (now − MinValue wraps negative): the store never read, so All apps and Windows search's home stood empty.
    private long? readAt;
    private bool reading;
    private int version;
    private IReadOnlyList<StartApp> apps = [];
    private IReadOnlyList<StartRecommendation> files = [];
    private IReadOnlyList<StartRecommendation> folders = [];
    private StartUses uses;
    private StartAccount account = new(Environment.UserName, null);
    private bool updateDue;
    private QuickLinkMachine power = new(false, false, true, false);

    internal StartStore(StartReads reads)
    {
        this.reads = reads;
        uses = ReadUses();
    }

    public int Version => Volatile.Read(ref version);

    public IReadOnlyList<StartApp> Apps => apps;

    public IReadOnlyList<StartRecommendation> Files => files;

    public IReadOnlyList<StartRecommendation> Folders => folders;

    public StartUses Uses => uses;

    public StartAccount Account => account;

    public bool UpdateDue => updateDue;

    public QuickLinkMachine Power => power;

    public event Action? Changed;

    /// <summary>Karpo's record of what was opened from Start: on this PC, never roamed.</summary>
    private static string UsesFile => Path.Combine(Config.LocalFolder, "start.tsv");

    public void Refresh()
    {
        lock (gate)
        {
            if (reading || (readAt is { } at && reads.Ticks() - at < Kept.TotalMilliseconds))
            {
                return;
            }
            reading = true;
        }
        reads.Run(() =>
        {
            try
            {
                Read();
            }
            finally
            {
                lock (gate)
                {
                    reading = false;
                    readAt = reads.Ticks();
                }
            }
            Interlocked.Increment(ref version);
            Changed?.Invoke();
        });
    }

    /// <summary>Everything, on the reading thread; a part that fails keeps what it had.</summary>
    private void Read()
    {
        Troubles.Guard("Start: the apps", () =>
        {
            apps = [.. reads.Apps().Select(app => new StartApp(app.Name, app.Target))];
            lock (gate)
            {
                if (uses.Saw(apps.Select(app => app.Target), Clock.RealUtc()))
                {
                    WriteUses();
                }
            }
        });
        Troubles.Guard("Start: the recent files", () =>
        {
            var recent = reads.Recent();
            files = [.. recent.Where(item => !item.Folder).Take(MostFiles).Select(item => new StartRecommendation(Path.GetFileName(item.Path), item.Path, item.UsedUtc, App: false))];
            folders = [.. recent.Where(item => item.Folder).Take(MostFiles).Select(item => new StartRecommendation(Path.GetFileName(item.Path.TrimEnd('\\')) is { Length: > 0 } name ? name : item.Path, item.Path, item.UsedUtc, App: false))];
        });
        Troubles.Guard("Start: the account", () => account = reads.Account());
        Troubles.Guard("Start: Windows Update", () => updateDue = reads.UpdateDue());
        Troubles.Guard("Start: the power menu", () => power = reads.Power());
    }

    public void Opened(string target)
    {
        lock (gate)
        {
            uses.Opened(target, Clock.RealUtc());
            WriteUses();
        }
        Interlocked.Increment(ref version);
    }

    public Pixels? Icon(string target, float px, Action came) => TargetPictures.Sized.Get(PinTargets.PictureOf(target), came, px);

    private static StartUses ReadUses()
    {
        try
        {
            return DurableFile.ReadText(UsesFile, out var text) == FileRead.Ok ? StartUses.Read(text) : new StartUses();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new StartUses();
        }
    }

    private void WriteUses()
    {
        try
        {
            Directory.CreateDirectory(Config.LocalFolder);
            DurableFile.Write(UsesFile, uses.Write(), keepBackup: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Troubles.Report("Start: writing what was opened", error);
        }
    }

    /// <summary>The account: an isolated run's (a test, --smoke) a made-up one, never the person's.</summary>
    private static StartAccount ReadAccount()
    {
        if (Config.IsIsolated)
        {
            return StartSamples.SampleAccount;
        }
        return new StartAccount(DisplayName() ?? Environment.UserName, Picture());
    }

    /// <summary>The account's display name ("Alex Morgan"), as Windows has it; null where it has none (a local account without one).</summary>
    private static unsafe string? DisplayName()
    {
        const int nameDisplay = 3;
        var size = 256u;
        var buffer = stackalloc char[(int)size];
        return GetUserNameExW(nameDisplay, buffer, &size) != 0 && size > 0 ? new string(buffer, 0, (int)size).Trim() is { Length: > 0 } name ? name : null : null;
    }

    /// <summary>The account's picture as Windows keeps it for its sign-in screen and Start, the largest it has up to 448 px; null for none.</summary>
    private static Pixels? Picture()
    {
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        if (sid is null)
        {
            return null;
        }
        using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}"); // not-ui: a registry path
        foreach (var name in new[] { "Image192", "Image240", "Image208", "Image96", "Image448" })
        {
            if (key?.GetValue(name) is string path && File.Exists(path))
            {
                return Favicons.FaviconImage.Decode(File.ReadAllBytes(path), PictureSide);
            }
        }
        return null;
    }

    [DllImport("secur32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe byte GetUserNameExW(int format, char* name, uint* size);
}
