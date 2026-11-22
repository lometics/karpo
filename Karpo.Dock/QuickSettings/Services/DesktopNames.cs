// A virtual desktop's name, for the rename flyout: read from where Windows keeps
// it (the desktop's key under Explorer's VirtualDesktops), and set through the
// same call Task View makes (DesktopControl.Rename).

using Microsoft.Win32;

namespace Karpo.Dock.QuickSettings.Services;

internal interface IDesktopNames
{
    /// <summary>The desktop's name as the person set it; empty for one Windows names by number; null when it is gone.</summary>
    string? NameOf(Guid desktop);

    /// <summary>Names the desktop; false when Windows didn't.</summary>
    bool Rename(Guid desktop, string name);
}

internal sealed class WindowsDesktopNames : IDesktopNames
{
    private const string Desktops = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops\Desktops\";

    public string? NameOf(Guid desktop)
    {
        if (desktop == Guid.Empty)
        {
            return null;
        }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Desktops + desktop.ToString("B"));
            // A desktop that was never renamed may have no key of its own yet.
            return key?.GetValue("Name") as string ?? "";
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return "";
        }
    }

    public bool Rename(Guid desktop, string name) => desktop != Guid.Empty && DesktopControl.Rename(desktop, name);
}

/// <summary>A made-up desktop for pictures and tests: renaming only pretends.</summary>
internal sealed class SampleDesktopNames : IDesktopNames
{
    private string name = "Work";

    /// <summary>The last name given, for tests.</summary>
    public string Name => name;

    public string? NameOf(Guid desktop) => name;

    public bool Rename(Guid desktop, string value)
    {
        name = value.Trim();
        return name.Length > 0;
    }
}
