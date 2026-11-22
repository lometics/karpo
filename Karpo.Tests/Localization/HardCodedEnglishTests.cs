// No English written in the code: every word Karpo shows comes from Karpo.Core\Strings\en.json
// through S, so that it is translated. A ratchet over the C#: it counts what is still written out and
// holds the count to its baseline: it may only go down. A change that writes new English fails
// here; a change that moves English into en.json lowers the count, and the baseline goes down with it
// (the test says to what), so it can never creep back. It is 0 since the i18n audit (docs\widgets.md,
// "Karpo's words").
//
// What counts in C#: a string literal (or an interpolated string's opening words) that starts with
// a capital letter and has a space in it — "Open Calendar", $"Volume {level}" —, and any words put
// straight into a control's Text, Header, Content, tip or screen-reader name. What doesn't:
//   - logs: an argument of Troubles.*, Console.*, Trace.*, Debug.* or appbarTrace.*, Smoke.cs and Stage.cs;
//   - an exception's message (in a throw, or in new …Exception(…));
//   - an attribute's argument;
//   - a line marked "// not-ui": a window class, a title Windows gives, a name matched against
//     another app's, anything that is a name rather than words for the person to read.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Karpo.Tests.Localization;

public sealed class HardCodedEnglishTests
{
    /// <summary>English literals in Karpo's C#: 1327 when the strings table came in (G57 chunk A), 731 after the dock's menus and flyouts (chunk C), 304 after the widgets, the registries and the widget faces (chunk D), 250 after Localization, Accounts, About and Taskbar menu (G58 chunk 3), 247 once the widgets' own update intervals went (G61), 237 after General and Displays (G58 chunk 2), 233 after the settings window's chrome (G57 B1), 232 once the island backgrounds went (G64), 112 after Layouts and the pages of one widget, folder, pin and the tray (G58 chunk 4), 2026-10-05, 108 once the layouts went and with them the import summary's names for their settings (one configuration), 2026-10-07, 106 once the taskbar menu was fixed, 0 once the icon gallery, the import summary and the settings app's problems went and every word put straight into a control counted (the i18n audit), 2026-10-12. Only ever lowered.</summary>
    private const int CodeBaseline = 0;

    /// <summary>The projects whose code shows words to the person.</summary>
    private static readonly string[] Projects = ["Karpo.Core", "Karpo.Dock", "Karpo.Accounts"];

    /// <summary>What a log is written with: its arguments are for whoever reads karpo.log, in English.</summary>
    private static readonly HashSet<string> Logs = new(StringComparer.Ordinal) { "Troubles", "Console", "Trace", "trace", "Debug", "appbarTrace", "AppbarTrace" };

    /// <summary>
    /// Files that write nothing for the person: the --smoke run's report and the --stage run's; and §635 the samples' made-up
    /// data — places, teams, shares, headlines, meetings, mail and people, §643 a share, a camera, a note and tasks — names and not Karpo's words.
    /// </summary>
    private static readonly HashSet<string> NotShown = new(StringComparer.OrdinalIgnoreCase)
    {
        "Smoke.cs", "Stage.cs", "Samples.Accounts.cs", "Samples.Forecasts.cs", "Samples.Markets.cs", "Samples.Sports.cs", "Samples.Defaults.cs",
    };

    [Fact]
    public void NoNewEnglishIsWrittenInTheCode()
    {
        var found = CodeEnglish();
        Ratchet("C# literals", found, CodeBaseline, nameof(CodeBaseline));
    }

    [Fact]
    public void TheCountTellsWordsFromNames()
    {
        const string code = """
            class Sample
            {
                [Description("Shown in an attribute")]
                void Run(int level)
                {
                    Show("Open Calendar");
                    Show($"Volume {level}%");
                    Show("lowercase words");
                    Show("Single");
                    Show($"{level} Items here");
                    Troubles.Write("A log line");
                    Console.Error.WriteLine("Another log line");
                    Karpo.Core.Troubles.Report("A log with its namespace", null);
                    throw new InvalidOperationException("An exception message");
                    Show("Shell_TrayWnd Class"); // not-ui
                    Show(S.DockVolumeLevel(level));
                    System.Diagnostics.Debug.WriteLine("A log with System's namespace");
                    box.PlaceholderText = "search";
                    AutomationProperties.SetName(box, "Search");
                    box.Tag = "Some tag";
                    box.Name = "box";
                }
            }
            """;
        var found = Count(CSharpSyntaxTree.ParseText(code), "Sample.cs");
        Assert.Equal(["Open Calendar", "Volume ", "search", "Search", "Some tag"], found.Select(hit => hit.Text));
    }

    // --- C# -----------------------------------------------------------------------------------------

    private sealed record Hit(string File, int Line, string Text);

    private static List<Hit> CodeEnglish()
    {
        var repo = StringFiles.Repository;
        var hits = new List<Hit>();
        foreach (var project in Projects)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(repo, project), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repo, file);
                if (relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || NotShown.Contains(Path.GetFileName(file)))
                {
                    continue;
                }
                hits.AddRange(Count(CSharpSyntaxTree.ParseText(File.ReadAllText(file)), relative));
            }
        }
        return hits;
    }

    private static List<Hit> Count(SyntaxTree tree, string file)
    {
        var hits = new List<Hit>();
        var text = tree.GetText();
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            string? words = node switch
            {
                LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
                    when literal.Parent is not InterpolationSyntax => literal.Token.ValueText,
                InterpolatedStringExpressionSyntax interpolated
                    => interpolated.Contents.FirstOrDefault() is InterpolatedStringTextSyntax first ? first.TextToken.ValueText : null,
                _ => null,
            };
            if (words is null || !(IsEnglish(words) || (words.Any(char.IsLetter) && ShownAt(node))) || Excluded(node, text))
            {
                continue;
            }
            hits.Add(new Hit(file, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, words));
        }
        return hits;
    }

    /// <summary>The properties a control shows its words in.</summary>
    private static readonly HashSet<string> ShownIn = new(StringComparer.Ordinal)
    {
        "Text", "Header", "Content", "PlaceholderText", "Title", "Subtitle", "Description", "Message", "Label", "ToolTip",
        "PrimaryButtonText", "SecondaryButtonText", "CloseButtonText", "OnContent", "OffContent",
    };

    /// <summary>The calls that give a control its words: a tip, a screen reader's name.</summary>
    private static readonly HashSet<string> ShownBy = new(StringComparer.Ordinal) { "SetName", "SetHelpText", "SetToolTip", "SetTitle", "SetText" };

    /// <summary>
    /// Whether a literal is put straight where a person reads it — Text = "Done",
    /// SetToolTip(button, "back") —, which counts whatever its case and however many words.
    /// </summary>
    private static bool ShownAt(SyntaxNode node) => node.Parent switch
    {
        AssignmentExpressionSyntax assignment when assignment.Right == node => ShownIn.Contains(Name(assignment.Left)),
        ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax call } } => ShownBy.Contains(Name(call.Expression)),
        _ => false,
    };

    private static string Name(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => "",
    };

    /// <summary>Words for a person: a capital first, and a space.</summary>
    private static bool IsEnglish(string words)
    {
        var trimmed = words.TrimStart();
        return trimmed.Length > 1 && char.IsUpper(trimmed[0]) && trimmed.Contains(' ');
    }

    private static bool Excluded(SyntaxNode node, Microsoft.CodeAnalysis.Text.SourceText text)
    {
        var span = node.GetLocation().GetLineSpan();
        for (var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)
        {
            if (text.Lines[line].ToString().Contains("// not-ui", StringComparison.Ordinal))
            {
                return true;
            }
        }
        foreach (var around in node.Ancestors())
        {
            switch (around)
            {
                case AttributeSyntax:
                case ThrowStatementSyntax:
                case ThrowExpressionSyntax:
                    return true;
                case ObjectCreationExpressionSyntax creation when creation.Type.ToString().EndsWith("Exception", StringComparison.Ordinal):
                    return true;
                case InvocationExpressionSyntax invocation when Logs.Contains(Leftmost(invocation.Expression)):
                    return true;
                case MemberDeclarationSyntax:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// "Troubles" for Troubles.Write, "Console" for Console.Error.WriteLine, and "Troubles" too for
    /// Karpo.Core.Troubles.Report, whose namespace is written out where TerraFX's names would clash.
    /// </summary>
    private static string Leftmost(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax { Expression: var left, Name: var name } when Namespace(left) => name.Identifier.ValueText,
        MemberAccessExpressionSyntax member => Leftmost(member.Expression),
        IdentifierNameSyntax name => name.Identifier.ValueText,
        _ => "",
    };

    /// <summary>Whether <paramref name="expression"/> is a namespace written out before a log: "Karpo", "Karpo.Core", "System.Diagnostics".</summary>
    private static bool Namespace(ExpressionSyntax expression) => expression.ToString() is "Karpo" or "Karpo.Core" or "Karpo.Dock" or "System.Diagnostics";

    // --- The ratchet --------------------------------------------------------------------------------

    private static void Ratchet(string what, List<Hit> found, int baseline, string constant)
    {
        var worst = string.Join("\n", found
            .GroupBy(hit => hit.File)
            .OrderByDescending(group => group.Count())
            .Take(12)
            .Select(group => $"  {group.Count(),4}  {group.Key}"));
        Assert.True(
            found.Count <= baseline,
            $"{found.Count} {what} written in English, more than the {baseline} allowed: put the new words in "
            + $"Karpo.Core\\Strings\\en.json and use S (docs\\widgets.md, \"Karpo's words\"), or mark a name \"// not-ui\".\nMost:\n{worst}");
        Assert.True(
            found.Count == baseline,
            $"{found.Count} {what} written in English, fewer than the {baseline} allowed: lower {constant} in "
            + $"HardCodedEnglishTests.cs to {found.Count}, so they stay gone.");
    }
}
