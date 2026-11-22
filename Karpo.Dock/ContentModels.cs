using Karpo.Core;
namespace Karpo.Dock;

/// <param name="Id">
/// Windows' own id for the desktop, so a row can be the same row after the desktops are reordered:
/// keyed on where it sits, a row that moved would be a new row and would take the pointer and the
/// keyboard away from the desktop the person just moved. Empty where nobody knows it (a test).
/// </param>
public sealed record DesktopButton(string Name, IReadOnlyList<Item> Apps, int MoreApps, string Id = "");
public sealed record DesktopStrip(IReadOnlyList<DesktopButton> Buttons, int Current)
{
    public IReadOnlyList<string> Names => Buttons.Select(button => button.Name).ToArray();
    public static implicit operator DesktopStrip((IReadOnlyList<string> Names, int Current) value) =>
        new(value.Names.Select(name => new DesktopButton(name, [], 0)).ToArray(), value.Current);
}

/// <summary>The dock's island objects, by the key of the part that shows each ("{owner}/{id}").</summary>
public sealed class IslandCollection : Dictionary<string, IIsland>
{
    public IIsland? Find(PartSpec part) => this.GetValueOrDefault(part.Key);
}