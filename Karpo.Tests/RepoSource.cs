// The repository's own source, for the tests that check Karpo as it is written: a call made where it is
// meant, nothing left that starts what is gone. Found from this file's path at build time; empty where the
// source isn't there.

using System.Runtime.CompilerServices;

namespace Karpo.Tests;

internal static class RepoSource
{
    /// <summary>The repository, from this file's own path (it stands in Karpo.Tests); empty when the source isn't there.</summary>
    public static string Root() => Above(Here());

    private static string Here([CallerFilePath] string here = "") => here;

    private static string Above(string file)
    {
        var repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file) ?? ".", ".."));
        return File.Exists(Path.Combine(repo, "Karpo.slnx")) ? repo : "";
    }

    /// <summary>A file of the repository, by its path's <paramref name="parts"/>.</summary>
    public static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    /// <summary>Every C# file of <paramref name="project"/> (a folder of the repository), its build folders left out.</summary>
    public static IEnumerable<string> CSharp(string project)
    {
        var folder = Path.Combine(Root(), project);
        var built = new[] { "bin", "obj" }.Select(name => Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar).ToArray();
        return Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !built.Any(part => path[folder.Length..].Contains(part, StringComparison.OrdinalIgnoreCase)));
    }
}
