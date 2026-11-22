// §642: Windows search's own reading of Windows. The default tests never ask the real index (WinSearchEngineTests hand in
// a fake). With KARPO_INDEX_TESTS=1 (by hand, on a PC whose Windows Search runs) the real index is asked read-only — a
// question made by WinSearchQuery, its rows read through the OLE DB accessor, a cancel ending one; nothing is opened,
// launched or written. §655: what Windows indexes is no longer read (the line's page links to Searching Windows).

using System.Diagnostics;
using Karpo.Dock.WinSearch;
using Xunit.Abstractions;

namespace Karpo.Tests.Searching;

/// <summary>A fact that asks the real index, read only: only with KARPO_INDEX_TESTS=1.</summary>
public sealed class IndexFactAttribute : FactAttribute
{
    public IndexFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KARPO_INDEX_TESTS") != "1")
        {
            Skip = "Asks this PC's Windows Search index, read only; set KARPO_INDEX_TESTS=1.";
        }
    }
}

public sealed class WindowsIndexTests(ITestOutputHelper output)
{
    private static WinSearchIndexAnswer Ask(string typed, out TimeSpan took)
    {
        var sql = WinSearchQuery.Sql(WinSearchQuery.Parse(typed), contents: true, top: WinSearchEngine.MostRows)!;
        var watch = Stopwatch.StartNew();
        WinSearchIndexAnswer? answer = null;
        // As the dock asks: on a thread of its own, COM up.
        Karpo.Dock.Drives.DriveReader.Start(() => answer = new WindowsIndex().Query(sql, CancellationToken.None)).Wait();
        took = watch.Elapsed;
        return answer!;
    }

    [IndexFact]
    public void TheRealIndexAnswersReadOnly()
    {
        foreach (var typed in new[] { "windows", "folders: users", "photos: a", "documents: readme" })
        {
            var answer = Ask(typed, out var took);
            output.WriteLine($"{typed}: {answer.Rows.Count} rows in {took.TotalMilliseconds:0} ms, problem {answer.Problem ?? "none"}");
            foreach (var row in answer.Rows.Take(5))
            {
                output.WriteLine($"  {WinSearchRank.CategoryOf(row),-9} {row.Name} | {row.Path} | {row.Url} | {row.Type} | {row.Modified:u} | {row.Size} | {row.Rank}");
            }
            Assert.Null(answer.Problem);
        }
    }

    [IndexFact]
    public void ACancelEndsTheRealQuestion()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        WinSearchIndexAnswer? answer = null;
        Karpo.Dock.Drives.DriveReader.Start(() => answer = new WindowsIndex().Query(WinSearchQuery.Sql(WinSearchQuery.Parse("a"), true, 60)!, cancel.Token)).Wait();
        Assert.Empty(answer!.Rows);
    }
}
