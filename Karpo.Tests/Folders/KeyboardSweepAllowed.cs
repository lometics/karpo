// keys: the kinds the keys do not walk whole yet (KeyboardSweepTests), each with what is missing; a kind that is
// walked now fails the test, so the list only shrinks. Empty since §471: every target of every kind is reached by
// the keys; a kind that breaks that is mended, not written in here.

namespace Karpo.Tests.Folders;

public sealed partial class KeyboardSweepTests
{
    /// <summary>Kind → what the keys still miss in its line.</summary>
    internal static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal);
}
