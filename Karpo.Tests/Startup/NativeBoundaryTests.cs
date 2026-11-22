// What crosses between Karpo and Windows without a managed caller to catch what goes wrong.
//
// An exception that leaves an [UnmanagedCallersOnly] method (a window procedure, a hook, a COM sink,
// an enumeration callback) or the top of a thread of Karpo's own is not caught by anybody: the process
// ends with a fail-fast (0xC0000409), and the whole bar with it. Several of these run whenever any app
// starts (the tray spy hears every app's appbar and icon calls, the taskbar list's band every app's
// buttons, the show hook every window), so one that throws looks like "starting any app crashes Karpo".
//
// And a P/Invoke whose DLL does not export it throws EntryPointNotFoundException at its first call:
// Network Properties' dialog was imported from wlanapi.dll, which has no WlanUIEditProfile on Windows 11
// (wlanui.dll has), and that ended the dock three times in a minute on 28 September (fixed with the
// dialog in a process of its own, WifiProperties.cs). Every import is checked the same way here.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.Startup;

public sealed class NativeBoundaryTests
{
    /// <summary>
    /// The callbacks that call nothing that can throw: COM's reference counting (Interlocked and a
    /// free), interface answers made of pointer compares, constant answers, and window procedures that
    /// only hand on to DefWindowProc. Anything else must catch.
    /// </summary>
    private static readonly HashSet<string> CannotThrow =
    [
        "Accessibility/Uia.cs AddRef",
        "Accessibility/Uia.cs ReleaseThis",
        "Accessibility/Uia.cs ProviderOptions",
        "Accessibility/Uia.cs EmbeddedRoots",
        // §520: a side item can't be taken out of the selection; it answers a constant.
        "Accessibility/Uia.cs RemoveFromSelection",
        "DropTarget.cs QueryInterface",
        "DropTarget.cs AddRef",
        "DropTarget.cs Release",
        "LivePeek.cs Procedure",
        "QuickSettings/Services/Sinks.cs QueryInterface",
        "QuickSettings/Services/Sinks.cs AddRef",
        "QuickSettings/Services/Sinks.cs Release",
        "QuickSettings/Services/Sinks.cs PropertyValueChanged",
        "QuickSettings/Services/Sinks.cs ChannelVolumeChanged",
        "QuickSettings/Services/Sinks.cs GroupingChanged",
        "SystemStatus.cs QueryInterface",
        "SystemStatus.cs AddRef",
        "SystemStatus.cs Release",
        "SystemStatus.cs PropertyValueChanged",
        "Tray.cs PlainProcedure",
    ];

    /// <summary>
    /// Entry points that are allowed to be missing on some Windows: each call site catches
    /// EntryPointNotFoundException and goes without.
    /// </summary>
    private static readonly HashSet<string> MayBeMissing =
    [
    ];

    /// <summary>
    /// Every [UnmanagedCallersOnly] method in the dock and the core catches every exception, or is one
    /// of those that call nothing that can throw. A new callback without a catch fails here.
    /// </summary>
    [Fact]
    public void NoCallbackFromWindowsLetsAnExceptionOut()
    {
        if (Source() is not { Length: > 0 } repo)
        {
            return;
        }
        var found = new List<string>();
        var unguarded = new List<string>();
        foreach (var project in new[] { "Karpo.Dock", "Karpo.Core" })
        {
            var root = Path.Combine(repo, project);
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }
                var name = Path.GetRelativePath(root, file).Replace('\\', '/');
                foreach (var (method, body) in Callbacks(File.ReadAllText(file)))
                {
                    var key = $"{name} {method}";
                    found.Add(key);
                    if (!CannotThrow.Contains(key) && !Catches(body))
                    {
                        unguarded.Add(key);
                    }
                }
            }
        }
        // The scan itself works: the dock's own window procedure is one of what it found.
        Assert.Contains("Dock.cs WndProc", found);
        Assert.True(found.Count > 50, $"only {found.Count} callbacks found");
        Assert.True(unguarded.Count == 0, "callbacks that let an exception out: " + string.Join(", ", unguarded));
        // And the list of those that cannot throw names only callbacks that still exist.
        Assert.Empty(CannotThrow.Except(found));
    }

    /// <summary>
    /// Every place managed code starts with nobody above it to catch: a timer's callback, a thread's
    /// body, a wait the pool answers, a file watcher's event, an <c>async void</c>, and the work a
    /// task is started with. Each catches at its top, or hands its work to <c>Troubles.Guard</c>,
    /// which does. The callback is read where it is written, or in the method of the same file it
    /// names. A new one without a catch fails here, as AccountSet.Reload did (an exception in a
    /// timer's callback on a pool thread ends the process).
    /// </summary>
    [Fact]
    public void NoThreadOfKarposLetsAnExceptionOut()
    {
        if (Source() is not { Length: > 0 } repo)
        {
            return;
        }
        var found = new List<string>();
        var unguarded = new List<string>();
        foreach (var project in new[] { "Karpo.Dock", "Karpo.Core", "Karpo.Accounts" })
        {
            var root = Path.Combine(repo, project);
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }
                var name = $"{project}/{Path.GetRelativePath(root, file).Replace('\\', '/')}";
                var code = File.ReadAllText(file);
                foreach (var (kind, callback) in Starts(code))
                {
                    var key = $"{name} {kind} {Named(callback) ?? "lambda"}";
                    found.Add(key);
                    if (!StartGuarded.ContainsKey(key) && !Guarded(code, callback))
                    {
                        unguarded.Add(key);
                    }
                }
            }
        }
        // The scan itself works: the accounts' timer and the clipboard's chain are among what it found.
        Assert.Contains(found, key => key.StartsWith("Karpo.Accounts/AccountBook.cs timer ", StringComparison.Ordinal));
        Assert.Contains(found, key => key.StartsWith("Karpo.Dock/Pim/AccountSet.cs event ", StringComparison.Ordinal));
        // And a continuation chained to a call, and what a dispatcher queue and its timer run.
        Assert.Contains(found, key => key.StartsWith("Karpo.Dock/QuickSettings/Services/Wifi.cs continuation ", StringComparison.Ordinal));
        Assert.Contains(found, key => key.StartsWith("Karpo.Dock/QuickSettings/Services/DoNotDisturb.cs queue ", StringComparison.Ordinal));
        Assert.Contains(found, key => key.StartsWith("Karpo.Dock/QuickSettings/Services/Feedback.cs queue ", StringComparison.Ordinal));
        Assert.True(found.Count > 60, $"only {found.Count} starts found");
        Assert.True(unguarded.Count == 0, "starts that let an exception out: " + string.Join(", ", unguarded));
        Assert.Empty(StartGuarded.Keys.Except(found));
    }

    /// <summary>
    /// Starts that need no catch of their own, and why. Each names its file, its kind and what it runs.
    /// </summary>
    private static readonly Dictionary<string, string> StartGuarded = new()
    {
        ["Karpo.Dock/Dock.WindowsPins.cs watcher Heard"] = "posts a message, which does not throw",
        ["Karpo.Dock/Drives/DriveWatch.cs task ReadRemote"] = "ReadRemote catches, and its result is read (Collect)",
        ["Karpo.Dock/Tray.cs wait Changed"] = "marks the promotions stale and posts a message, neither of which throws",
        ["Karpo.Dock/PreviewThumbnails.cs task lambda"] = "walks an array, and each unregistering in it is Guarded",
        ["Karpo.Dock/MediaSessions.cs queue lambda"] = "an exchange, then Invoke, which catches",
        ["Karpo.Core/OptionFields.cs task Ticked"] = "Find catches the disk's errors, and the search reads the task's result and its failure (MenuSearch.Answer)",
        // §338, §339: what these run cannot fail, or fails into a task whose failure is read.
        ["Karpo.Dock/Dock.License.cs continuation lambda"] = "keeps words of the licence: a completed task's result (OnlyOnRanToCompletion) or the status, through LicenseView's pure words",
        ["Karpo.Dock/Dock.MenuAccounts.cs task lambda"] = "returns the task its failure goes into, which AccountForm's continuation reads (done.Exception)",
        ["Karpo.Core/AccountForm.cs continuation post"] = "posts to the dock's queue, whose work catches; the try's failure is read off the task",
    };

    /// <summary>
    /// Where managed code starts with nobody above it, in <paramref name="code"/>: what kind of start,
    /// and the callback as written (a lambda, or a method's name).
    /// </summary>
    private static IEnumerable<(string Kind, string Callback)> Starts(string code)
    {
        code = Regex.Replace(code, @"//[^\n]*", "");
        foreach (Match start in Regex.Matches(code, @"new (?:System\.Threading\.)?(Timer|Thread)\("))
        {
            yield return (start.Groups[1].Value == "Timer" ? "timer" : "thread", Argument(code, start.Index + start.Length, 0));
        }
        foreach (Match start in Regex.Matches(code, @"RegisterWaitForSingleObject\("))
        {
            yield return ("wait", Argument(code, start.Index + start.Length, 1));
        }
        // A continuation is chained to a call as often as to a name (Set(on).ContinueWith( as well
        // as working.ContinueWith(): no word boundary before it.
        foreach (Match start in Regex.Matches(code, @"(?:\bTask\.Run|\bTask\.Factory\.StartNew|\.ContinueWith)\("))
        {
            yield return (start.Value.StartsWith('.') ? "continuation" : "task", Argument(code, start.Index + start.Length, 0));
        }
        // What a dispatcher queue runs, and its timer's ticks: a handler that throws there is
        // reported to Windows as unhandled, and that ends the process too.
        foreach (Match start in Regex.Matches(code, @"\.TryEnqueue\("))
        {
            var first = Argument(code, start.Index + start.Length, 0);
            yield return ("queue", first.Contains("Priority", StringComparison.Ordinal) ? Argument(code, start.Index + start.Length, 1) : first);
        }
        foreach (Match handler in Regex.Matches(code, @"\b\w+\.Tick \+= ([^;]+);"))
        {
            yield return ("queue", handler.Groups[1].Value.Trim());
        }
        // A file watcher's events, on the variables and fields this file makes watchers in.
        var watchers = Regex.Matches(code, @"(\w+) = new FileSystemWatcher\b").Select(made => made.Groups[1].Value).ToHashSet();
        foreach (Match handler in Regex.Matches(code, @"\b(\w+)\.(?:Created|Deleted|Changed|Renamed|Error) \+= ([^;]+);"))
        {
            if (watchers.Contains(handler.Groups[1].Value))
            {
                yield return ("watcher", handler.Groups[2].Value.Trim());
            }
        }
        // What the accounts' watch says, raised on its timer's thread.
        foreach (Match handler in Regex.Matches(code, @"\bwatch\.Changed \+= ([^;]+);"))
        {
            yield return ("event", handler.Groups[1].Value.Trim());
        }
        foreach (Match method in Regex.Matches(code, @"\basync void (\w+)\("))
        {
            yield return ("async-void", method.Groups[1].Value);
        }
    }

    /// <summary>The <paramref name="index"/>th argument of the call whose arguments start at <paramref name="at"/>.</summary>
    private static string Argument(string code, int at, int index)
    {
        var depth = 0;
        var from = at;
        var seen = 0;
        for (var i = at; i < code.Length; i++)
        {
            var c = code[i];
            if (c is '(' or '{' or '[')
            {
                depth++;
            }
            else if (c is ')' or '}' or ']')
            {
                if (depth == 0)
                {
                    return seen == index ? code[from..i].Trim() : "";
                }
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                if (seen == index)
                {
                    return code[from..i].Trim();
                }
                seen++;
                from = i + 1;
            }
        }
        return "";
    }

    /// <summary>
    /// The method a callback runs, where it runs one by name: a method group, or a lambda whose
    /// whole body is one call (<c>_ =&gt; Due()</c>, <c>static self =&gt; ((T)self!).ReadNow()</c>).
    /// Null for a lambda that does more.
    /// </summary>
    private static string? Named(string callback)
    {
        var body = Regex.Replace(callback, @"^(?:static\s+)?(?:async\s+)?(?:\([^)]*\)|\w+)\s*=>\s*", "");
        if (body.StartsWith('{'))
        {
            return null;
        }
        var call = Regex.Match(body, @"^(?:\(\([^)]*\)\w+!?\)\.|this\.)?(\w+)(\()?");
        if (!call.Success || call.Groups[1].Value == "Troubles")
        {
            return null;
        }
        if (!call.Groups[2].Success)
        {
            return call.Length == body.Length ? call.Groups[1].Value : null;
        }
        // One call, its arguments whatever they hold, and nothing after it.
        var depth = 0;
        for (var i = call.Length - 1; i < body.Length; i++)
        {
            depth += body[i] is '(' or '{' or '[' ? 1 : body[i] is ')' or '}' or ']' ? -1 : 0;
            if (depth == 0)
            {
                return i == body.Length - 1 ? call.Groups[1].Value : null;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether <paramref name="callback"/> catches every exception: handed to <c>Troubles.Guard</c>,
    /// a lambda with a catch at its top, or a call to a method of this file that has one.
    /// </summary>
    private static bool Guarded(string code, string callback)
    {
        var body = Regex.Replace(callback, @"^(?:static\s+)?(?:async\s+)?(?:\([^)]*\)|\w+)\s*=>\s*", "");
        if (body.StartsWith("Troubles.Guard(", StringComparison.Ordinal))
        {
            return true;
        }
        if (body.StartsWith('{'))
        {
            return Catches(body, allowOutOfMemory: true);
        }
        if (Named(callback) is not { } method)
        {
            return false;
        }
        foreach (Match declared in Regex.Matches(code, $@"^\s*(?:(?:private|internal|public|protected|static|async|unsafe|override)\s+)+[^\n(=]*?\b{method}\([^)]*\)\s*(=>|\{{|\n\s*\{{)", RegexOptions.Multiline))
        {
            var at = declared.Index + declared.Length;
            if (declared.Groups[1].Value == "=>")
            {
                if (code[at..code.IndexOf(';', at)].TrimStart().StartsWith("Troubles.Guard(", StringComparison.Ordinal))
                {
                    return true;
                }
                continue;
            }
            var open = code.LastIndexOf('{', at - 1);
            var depth = 0;
            var end = open;
            for (; end < code.Length; end++)
            {
                depth += code[end] == '{' ? 1 : code[end] == '}' ? -1 : 0;
                if (depth == 0)
                {
                    break;
                }
            }
            var inner = code[open..(end + 1)];
            if (Catches(inner, allowOutOfMemory: true) || Regex.IsMatch(inner, @"^\{\s*Troubles\.Guard\("))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Every DllImport in Karpo's own assemblies names a DLL that exports its entry point, on this
    /// Windows. A DLL this PC does not have is skipped; so are the entry points the code expects to be
    /// missing on some Windows.
    /// </summary>
    [Fact]
    public void EveryImportIsExportedByTheDllItNames()
    {
        var assemblies = new[] { typeof(Troubles).Assembly, typeof(WindowsWifi).Assembly };
        var missing = new List<string>();
        var checkedCount = 0;
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.GetCustomAttribute<DllImportAttribute>() is not { } import)
                    {
                        continue;
                    }
                    var entry = import.EntryPoint ?? method.Name;
                    var dll = import.Value;
                    if (MayBeMissing.Contains($"{dll} {entry}") || !TryLoad(dll, out var library))
                    {
                        continue;
                    }
                    checkedCount++;
                    if (entry.StartsWith('#'))
                    {
                        // By ordinal, as Windows' undocumented exports are.
                        if (GetProcAddress(library, nint.Parse(entry[1..])) == 0)
                        {
                            missing.Add($"{type.FullName}.{method.Name}: {dll}!{entry}");
                        }
                        continue;
                    }
                    if (!NativeLibrary.TryGetExport(library, entry, out _)
                        && !(!import.ExactSpelling && import.CharSet != CharSet.Ansi && NativeLibrary.TryGetExport(library, entry + "W", out _))
                        && !(!import.ExactSpelling && NativeLibrary.TryGetExport(library, entry + "A", out _)))
                    {
                        missing.Add($"{type.FullName}.{method.Name}: {dll}!{entry}");
                    }
                }
            }
        }
        Assert.True(checkedCount > 50, $"only {checkedCount} imports checked");
        Assert.True(missing.Count == 0, "imports whose DLL does not export them: " + string.Join(", ", missing));
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetProcAddress(nint module, nint ordinal);

    private static bool TryLoad(string dll, out nint library)
    {
        library = 0;
        // Only Windows' own: a DLL that would be looked for beside the program is not this test's.
        var path = Path.Combine(Environment.SystemDirectory, dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || dll.EndsWith(".drv", StringComparison.OrdinalIgnoreCase) ? dll : dll + ".dll");
        return File.Exists(path) && NativeLibrary.TryLoad(path, out library);
    }

    /// <summary>
    /// Whether <paramref name="body"/> catches every exception around its work: a catch of Exception,
    /// unfiltered, at the top of the method or inside one first test (a hook's <c>code &gt;= 0</c>).
    /// Catches deeper down, around one case of a switch, leave the rest of it open, as the tray spy's did.
    /// </summary>
    private static bool Catches(string body, bool allowOutOfMemory = false)
    {
        // A thread's start may let OutOfMemoryException by, as the dock's own catches do: the
        // process is past saving then.
        var pattern = allowOutOfMemory
            ? @"catch \(Exception(\s+(\w+))?\)(\s*when \(\2 is not OutOfMemoryException\))?\s*\n\s*\{"
            : @"catch \(Exception(\s+\w+)?\)\s*\n\s*\{";
        foreach (Match match in Regex.Matches(body, pattern))
        {
            var before = body[..match.Index];
            if (before.Count(c => c == '{') - before.Count(c => c == '}') <= 2)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Each [UnmanagedCallersOnly] method in <paramref name="code"/>: its name and its body (or expression).</summary>
    private static IEnumerable<(string Method, string Body)> Callbacks(string code)
    {
        foreach (Match attribute in Regex.Matches(code, @"^\s*\[UnmanagedCallersOnly[^\n]*\n", RegexOptions.Multiline))
        {
            var at = attribute.Index + attribute.Length;
            var signature = Regex.Match(code[at..], @"^\s*(?:private|internal|public)\s+static\s+[^(]*?\s(\w+)\(");
            Assert.True(signature.Success, "a callback whose signature could not be read: " + code.Substring(at, Math.Min(120, code.Length - at)));
            var method = signature.Groups[1].Value;
            var open = code.IndexOf('{', at);
            var arrow = code.IndexOf("=>", at, StringComparison.Ordinal);
            if (arrow >= 0 && (open < 0 || arrow < open))
            {
                yield return (method, code[arrow..code.IndexOf(';', arrow)]);
                continue;
            }
            var depth = 0;
            var end = open;
            for (; end < code.Length; end++)
            {
                depth += code[end] == '{' ? 1 : code[end] == '}' ? -1 : 0;
                if (depth == 0)
                {
                    break;
                }
            }
            yield return (method, code[open..(end + 1)]);
        }
    }

    /// <summary>The repository, when these tests run from inside it; empty otherwise.</summary>
    private static string Source()
    {
        for (var up = new DirectoryInfo(AppContext.BaseDirectory); up is not null; up = up.Parent)
        {
            if (File.Exists(Path.Combine(up.FullName, "Karpo.slnx")))
            {
                return up.FullName;
            }
        }
        return "";
    }
}
