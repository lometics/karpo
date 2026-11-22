// The flicker harness's fourth tier (docs\widgets.md §354): a source rule. Karpo's own windows are
// shown, hidden, raised and destroyed through Surfaces alone, so what a handler asks of them reaches
// the screen once its content has. A raw SetWindowPos, ShowWindow, DestroyWindow or DeferWindowPos
// anywhere else is a window that can come up empty again, or go before its replacement is drawn.
//
// §359: the list is Surfaces alone — what is not one of Karpo's shown windows (other apps', Explorer's
// taskbar, windows that never show, the peek's strip) has its own few calls there too, named for what
// they are — held to the count both ways: a call more fails, and so does one fewer.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Karpo.Tests.Present;

public sealed class RawWindowCallsTests
{
    /// <summary>The calls that act on a window's place in sight: showing, hiding, the z-order, its end.</summary>
    private static readonly HashSet<string> Raw = ["SetWindowPos", "ShowWindow", "DestroyWindow", "DeferWindowPos", "BeginDeferWindowPos", "EndDeferWindowPos", "SetWindowRgn"];

    /// <summary>The files of Karpo.Dock allowed raw calls, by their path under it, how many, and why.</summary>
    private static readonly Dictionary<string, (int Calls, string Why)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [@"Present\Surfaces.cs"] = (17, "the facade itself: Karpo's windows through the plan, and the few outside it (other apps' and Windows' notifications, a modal dialog brought back with its owner (§619), Explorer's taskbar and its blank region, windows that never show)"),
    };

    [Fact]
    public void KarposWindowsAreShownHiddenAndDestroyedThroughSurfacesAlone()
    {
        if (RepoSource.Root().Length == 0)
        {
            return;
        }
        var folder = Path.Combine(RepoSource.Root(), "Karpo.Dock");
        var found = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in RepoSource.CSharp("Karpo.Dock"))
        {
            var relative = Path.GetRelativePath(folder, file);
            var calls = Calls(File.ReadAllText(file));
            if (calls.Count > 0)
            {
                found[relative] = calls;
            }
        }
        var wrong = new List<string>();
        foreach (var (file, calls) in found)
        {
            if (!Allowed.TryGetValue(file, out var allowed))
            {
                wrong.Add($"{file}: {string.Join(", ", calls)} — show, hide, raise and destroy Karpo's windows through Surfaces");
            }
            else if (calls.Count != allowed.Calls)
            {
                wrong.Add($"{file}: {calls.Count} raw calls where {allowed.Calls} are allowed ({allowed.Why}): {string.Join(", ", calls)}"
                    + (calls.Count < allowed.Calls ? " — fewer: shrink the list" : ""));
            }
        }
        foreach (var (file, allowed) in Allowed)
        {
            if (!found.ContainsKey(file))
            {
                wrong.Add($"{file}: no raw call left of the {allowed.Calls} allowed: take it off the list");
            }
        }
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    /// <summary>
    /// Rule R2 (§355): the files that move or size a window of Karpo's (Surfaces.Place),
    /// how many calls and why. A popup's window stands still on its display (FlyoutHost: its frame, placed
    /// once per display), as do the windows that go with the pointer (§363, PointerWindow); what is left is a
    /// later phase's. Held both ways, so the list only shrinks.
    /// </summary>
    private static readonly Dictionary<string, (int Calls, string Why)> Placing = new(StringComparer.OrdinalIgnoreCase)
    {
        [@"FlyoutHost.cs"] = (1, "a popup's frame: its display, placed while out of sight"),
        [@"Dock.cs"] = (1, "a bar's window: its edge's strip at the most thickness, placed again only when its display or its edge's room changes (§358)"),
        [@"Dock.EditDeck.cs"] = (1, "edit mode's dim: its display, placed while out of sight"),
        [@"PointerWindow.cs"] = (1, "what goes with the pointer (the carry, the selection rectangle, the tray's carry): over every display, placed while out of sight, again only when the displays change (§363)"),
        [@"Preview.cs"] = (1, "the window list's own window, not a FlyoutHost yet"),
    };

    [Fact]
    public void OnlyTheListedFilesMoveOrSizeAWindow()
    {
        if (RepoSource.Root().Length == 0)
        {
            return;
        }
        var folder = Path.Combine(RepoSource.Root(), "Karpo.Dock");
        var wrong = new List<string>();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in RepoSource.CSharp("Karpo.Dock"))
        {
            var relative = Path.GetRelativePath(folder, file);
            if (relative.Equals(@"Present\Surfaces.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var calls = Calls(File.ReadAllText(file), new HashSet<string> { "Place" }, on: "Surfaces");
            if (calls.Count == 0)
            {
                continue;
            }
            found.Add(relative);
            if (!Placing.TryGetValue(relative, out var allowed))
            {
                wrong.Add($"{relative}: {string.Join(", ", calls)} — a window that moves while it shows is a frame out of step with its content (§355)");
            }
            else if (calls.Count != allowed.Calls)
            {
                wrong.Add($"{relative}: {calls.Count} calls where {allowed.Calls} are allowed ({allowed.Why}): {string.Join(", ", calls)}"
                    + (calls.Count < allowed.Calls ? " — fewer: shrink the list" : ""));
            }
        }
        wrong.AddRange(Placing.Keys.Where(file => !found.Contains(file)).Select(file => $"{file}: no call left: take it off the list"));
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public void TheRuleFindsCallsAndOnlyCalls()
    {
        const string code = """
            class Sample
            {
                void Act(nint w)
                {
                    _ = SetWindowPos(w, 0, 0, 0, 0, 0, 0);
                    Native.ShowWindow(w, 0);
                    var name = nameof(DestroyWindow);
                    // DestroyWindow(w) in a comment
                    var text = "ShowWindow(w)";
                    Surfaces.Show(w);
                }
            }
            """;
        Assert.Equal(["SetWindowPos @5", "ShowWindow @6"], Calls(code));
    }

    /// <summary>The raw calls <paramref name="code"/> makes, each by its name and line.</summary>
    private static List<string> Calls(string code) => Calls(code, Raw);

    /// <summary>The calls <paramref name="code"/> makes to <paramref name="names"/> (of <paramref name="on"/> only, where given), each by its name and line.</summary>
    private static List<string> Calls(string code, IReadOnlySet<string> names, string? on = null)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(call => on is null || call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner } && owner.Identifier.Text == on)
            .Select(call => (Call: call, Name: call.Expression switch
            {
                IdentifierNameSyntax name => name.Identifier.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                _ => "",
            }))
            .Where(entry => names.Contains(entry.Name))
            .Select(entry => $"{entry.Name} @{entry.Call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
            .ToList();
    }
}
