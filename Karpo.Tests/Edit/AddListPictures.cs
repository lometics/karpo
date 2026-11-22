// The look the pictures and the window tests are drawn in: Windows' own, dark or light, its accent the
// default blue (§576: the add list's own sheets went with its menu, every "+" opening the edit bar's add
// page, whose pictures are SettingsPanelSheets').

using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public static class AddListPictures
{
    /// <summary>Windows' look, <paramref name="dark"/> or light, its accent the default blue.</summary>
    internal static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));
}
