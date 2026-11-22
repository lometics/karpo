// The process's light or dark preference for what Windows itself draws in it.
// Karpo's own menus are flyouts (Flyout, FlyoutHost), never Win32 menus; the
// TrackPopupMenu path and its entries went when the last caller did.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public static unsafe class Menu
{
    /// <summary>
    /// Dark or light menus for the whole process. Windows has no documented
    /// switch for this: <c>SetPreferredAppMode</c> (uxtheme ordinal 135, since 1903)
    /// and <c>FlushMenuThemes</c> (136) are what Explorer-lookalikes use. A missing
    /// ordinal only means light menus.
    /// </summary>
    public static void Theme(bool dark)
    {
        // Never freed, like the Rust: the preference lives inside uxtheme, and
        // unloading it would forget the choice just made.
        HMODULE uxtheme;
        fixed (char* name = "uxtheme.dll")
        {
            uxtheme = LoadLibraryW(name);
        }
        if (uxtheme == HMODULE.NULL)
        {
            return;
        }
        var address = GetProcAddress(uxtheme, (sbyte*)135);
        if (address != null)
        {
            var setPreferredAppMode = (delegate* unmanaged<int, int>)address;
            // ForceDark and ForceLight: follow the dock's look, not a guess.
            _ = setPreferredAppMode(dark ? 2 : 3);
        }
        address = GetProcAddress(uxtheme, (sbyte*)136);
        if (address != null)
        {
            var flushMenuThemes = (delegate* unmanaged<void>)address;
            flushMenuThemes();
        }
    }
}
