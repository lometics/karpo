// §658: what Start (beta) and Windows search read of this PC, asked of the real shell, read only — the Apps folder, the
// Recent folder, Start's source, the index's first question, Karpo's own rows — each timed, so a reader that lists nothing
// or a step slow enough to freeze the dock shows. §661: the icons files wear, each its own at the size drawn, and the
// index's files by their addresses' paths. Only with KARPO_REAL_SHELL=1 (by hand); nothing is opened, launched or
// written but the test's own isolated folder.

using System.Diagnostics;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Dock.StartWidgets;
using Karpo.Dock.WinSearch;
using Xunit.Abstractions;

namespace Karpo.Tests.Searching;

/// <summary>A fact that reads this PC's shell, read only: only with KARPO_REAL_SHELL=1.</summary>
public sealed class RealShellFactAttribute : FactAttribute
{
    public RealShellFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KARPO_REAL_SHELL") != "1")
        {
            Skip = "Reads this PC's Apps folder, Recent folder and index, read only; set KARPO_REAL_SHELL=1.";
        }
    }
}

public sealed unsafe class RealShellTests(ITestOutputHelper output)
{
    private T Timed<T>(string what, Func<T> read, Func<T, string> said)
    {
        var watch = Stopwatch.StartNew();
        var value = read();
        output.WriteLine($"{what}: {said(value)} in {watch.Elapsed.TotalMilliseconds:0} ms");
        return value;
    }

    /// <summary>On a thread of its own with COM up, as the dock reads.</summary>
    private static T OffThread<T>(Func<T> read)
    {
        T value = default!;
        DriveReader.Start(() => value = read()).Wait();
        return value;
    }

    [RealShellFact]
    public void TheShellsReadersListThisPCs()
    {
        var apps = Timed("InstalledApps.List (first)", () => OffThread(InstalledApps.List), list => $"{list.Count} apps");
        Timed("InstalledApps.List (kept)", () => OffThread(InstalledApps.List), list => $"{list.Count} apps");
        var recent = Timed("RecentFiles.Items", () => OffThread(RecentFiles.Items), list => $"{list.Count(item => !item.Folder)} files, {list.Count(item => item.Folder)} folders");
        foreach (var app in apps.Take(5))
        {
            output.WriteLine($"  {app.Name} | {app.Target}");
        }
        Assert.NotEmpty(apps);
        Assert.NotEmpty(recent);
    }

    [RealShellFact]
    public void StartsSourceReadsAsALineShows()
    {
        using var came = new ManualResetEventSlim();
        var store = StartStore.Shared;
        store.Changed += came.Set;
        var watch = Stopwatch.StartNew();
        store.Refresh();
        var read = came.Wait(TimeSpan.FromSeconds(30));
        output.WriteLine($"StartStore.Refresh: read {read} in {watch.Elapsed.TotalMilliseconds:0} ms: {store.Apps.Count} apps, {store.Files.Count} files, {store.Folders.Count} folders");
        Assert.True(read);
        Assert.NotEmpty(store.Apps);
    }

    [RealShellFact]
    public void WindowsSearchsStepsTimed()
    {
        Timed("index, first question", () => OffThread(() => new WindowsIndex().Query(WinSearchQuery.Sql(WinSearchQuery.Parse("note"), true, 60)!, CancellationToken.None)), answer => $"{answer.Rows.Count} rows, {answer.Problem ?? "no problem"}");
        Timed("index, second question", () => OffThread(() => new WindowsIndex().Query(WinSearchQuery.Sql(WinSearchQuery.Parse("notes"), true, 60)!, CancellationToken.None)), answer => $"{answer.Rows.Count} rows");
        Timed("Karpo's own rows (WinSearchKarpoSource.Of)", () => WinSearchKarpoSource.Of(new Config()), found => $"{found.Count} rows");
        Timed("Karpo's own rows, again", () => WinSearchKarpoSource.Of(new Config()), found => $"{found.Count} rows");
        Timed("Settings' pages, first find", () => WinSearchPages.Find(WinSearchQuery.Parse("disp"), default(DateTime), null).ToList(), found => $"{found.Count} hits");
        Timed("calculator, first", () => WinSearchCalc.Answer("2+3", NumberMarks.Invariant), answer => answer?.Text ?? "none");
        Timed("contacts, first", () => Karpo.Dock.Pim.ContactStore.Shared.Contacts, list => $"{list.Count}");
        Timed("mail, first", () => Karpo.Dock.Pim.MailStore.Shared.Messages, list => $"{list.Count}");
        Timed("notes, first", () => Notebook.Shared.Current.Notes, list => $"{list.Count}");
        Timed("an app's icon", () => OffThread(() => Items.Icon(WinSearchPages.SettingsApp, 64)), icon => icon is null ? "none" : "one");
    }

    /// <summary>
    /// §661: the icon each file wears is its own, the one File Explorer shows, read at the size drawn: two programs two
    /// pictures (an .exe's own, not its kind's), Documents its own folder's (not a plain folder's), each at the size asked;
    /// and a known folder's display path (the index's "Public Documents") parses to nothing where its address's path does.
    /// </summary>
    [RealShellFact]
    public void EveryFileWearsItsOwnIconAtTheSizeItIsDrawn()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var notepad = OffThread(() => Items.Icon(Path.Combine(windows, "System32", "notepad.exe"), 24));
        var explorer = OffThread(() => Items.Icon(Path.Combine(windows, "explorer.exe"), 24));
        var program = OffThread(() => Items.KindIcon(".exe", 24));
        Assert.NotNull(notepad);
        Assert.NotNull(explorer);
        Assert.NotNull(program);
        Assert.Equal((24, 24), (notepad.Width, notepad.Height));
        Assert.False(notepad.Data.AsSpan().SequenceEqual(explorer.Data), "two programs wear one picture");
        Assert.False(notepad.Data.AsSpan().SequenceEqual(program.Data), "a program wears its kind's picture");
        Assert.Equal(48, OffThread(() => Items.Icon(Path.Combine(windows, "explorer.exe"), 48))!.Width);

        var documents = OffThread(() => Items.Icon(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 32));
        var folder = OffThread(() => Items.KindIcon("\\", 32));
        Assert.NotNull(documents);
        Assert.NotNull(folder);
        Assert.False(documents.Data.AsSpan().SequenceEqual(folder.Data), "Documents wears a plain folder's picture");

        var row = new Karpo.Core.WinSearchRow("Documents", @"C:\Users\Public\Public Documents", "file:C:/Users/Public/Documents", "Directory");
        Assert.Null(OffThread(() => Items.Icon(row.Path, 24)));
        Assert.NotNull(OffThread(() => Items.Icon(WinSearchRank.PathOf(row), 24)));
    }

    /// <summary>
    /// §661: the index's files on this PC, a few hundred: each one's address's path is one the shell parses (a file deleted
    /// since it was indexed aside), where its display path often is not — said, not held.
    /// </summary>
    [RealShellFact]
    public void TheIndexsFilesAreTheirAddressesPaths()
    {
        var rows = new List<Karpo.Core.WinSearchRow>();
        foreach (var word in new[] { "a", "e", "s", "doc", "public", "download" })
        {
            rows.AddRange(OffThread(() => new WindowsIndex().Query(WinSearchQuery.Sql(WinSearchQuery.Parse(word), false, 100)!, CancellationToken.None)).Rows);
        }
        var files = rows.Where(row => row.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)).DistinctBy(row => row.Url).ToList();
        int Unparsed(Func<Karpo.Core.WinSearchRow, string> path) => OffThread(() => files.Count(row =>
        {
            var item = Items.ShellItem(path(row));
            if (item == null)
            {
                return true;
            }
            item->Release();
            return false;
        }));
        var byAddress = Unparsed(WinSearchRank.PathOf);
        var byDisplay = Unparsed(row => row.Path);
        output.WriteLine($"{files.Count} files: {byAddress} by their address's path and {byDisplay} by their display path unparsed");
        Assert.True(byAddress <= Math.Max(1, files.Count / 100), $"{byAddress} of {files.Count} unparsed");
    }
}
