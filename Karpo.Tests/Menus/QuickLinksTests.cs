// The Start button's right-click menu (QuickLinks): Windows' Win+X lines in
// Windows' order, each with a glyph, and what each one opens.

using Karpo.Dock;

namespace Karpo.Tests.Menus;

public sealed class QuickLinksTests
{
    private static readonly QuickLinkMachine Desktop = new(Battery: false, WindowsTerminal: true, Sleep: true, Hibernate: false);

    private static IEnumerable<FlyoutItem> Lines(IEnumerable<FlyoutItem> items) =>
        items.SelectMany(item => item.Children is { } children ? [item, .. Lines(children)] : new[] { item });

    [Fact]
    public void A_desktop_PC_gets_Windows_lines_in_Windows_order()
    {
        var items = QuickLinks.Items(Desktop);
        var texts = items.Select(item => item.Separator ? "-" : item.Text).ToList();
        Assert.Equal(
        [
            "Installed apps", "Power Options", "Event Viewer", "System", "Device Manager", "Network Connections",
            "Disk Management", "Computer Management", "Terminal", "Terminal (Admin)", "-",
            "Task Manager", "Settings", "File Explorer", "Search", "Run", "-",
            "Shut down or sign out", "Desktop",
        ], texts);
        var power = items.Single(item => item.Text == "Shut down or sign out").Children;
        Assert.NotNull(power);
        Assert.Equal(["Sign out", "Sleep", "Shut down", "Restart"], power.Select(item => item.Text));
    }

    [Fact]
    public void Every_line_has_a_glyph()
    {
        var machine = new QuickLinkMachine(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: true);
        foreach (var item in Lines(QuickLinks.Items(machine)).Where(item => !item.Separator))
        {
            var glyph = Assert.IsType<FlyoutIcon.Glyph>(item.Icon);
            // One Segoe Fluent Icons code point, from its private use area.
            Assert.Single(glyph.Code);
            Assert.InRange(glyph.Code[0], '\uE000', '\uF8FF');
        }
    }

    [Fact]
    public void Ids_are_unique_and_name_their_lines()
    {
        var machine = new QuickLinkMachine(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: true);
        var lines = Lines(QuickLinks.Items(machine)).Where(item => !item.Separator).ToList();
        Assert.Equal(lines.Count, lines.Select(item => item.Id).Distinct().Count());
        foreach (var line in lines)
        {
            var link = QuickLinks.Of(line.Id);
            Assert.NotNull(link);
            Assert.Equal(line.Id, QuickLinks.IdOf(link.Value));
        }
        // Every line there is, on a PC that has them all. Lock is not one of Windows' own Win+X
        // lines: it belongs to the power widget's flyout (QuickLinks.PowerLines).
        Assert.Equal(
            // §641: nor are Start's Switch user and its two update rows (StartProfileLine.AccountRows, PowerRows).
            Enum.GetValues<QuickLink>().Where(link => link is not (QuickLink.Lock or QuickLink.SwitchUser or QuickLink.UpdateRestart or QuickLink.UpdateShutDown)).Order(),
            lines.Select(line => QuickLinks.Of(line.Id)!.Value).Order());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(QuickLinks.FirstId)]
    [InlineData(QuickLinks.FirstId + 25)]
    [InlineData(100u)]
    [InlineData(5000u)]
    public void Other_ids_are_no_line(uint id)
    {
        Assert.Null(QuickLinks.Of(id));
    }

    [Fact]
    public void A_laptop_gets_Mobility_Center_after_Installed_apps()
    {
        var items = QuickLinks.Items(Desktop with { Battery = true });
        Assert.Equal("Installed apps", items[0].Text);
        Assert.Equal("Mobility Center", items[1].Text);
        Assert.Equal("Power Options", items[2].Text);
        Assert.DoesNotContain(QuickLinks.Items(Desktop), item => item.Text == "Mobility Center");
    }

    [Fact]
    public void Without_Windows_Terminal_Windows_PowerShell_stands_in()
    {
        var machine = Desktop with { WindowsTerminal = false };
        var texts = QuickLinks.Items(machine).Select(item => item.Text).ToList();
        Assert.Contains("Windows PowerShell", texts);
        Assert.Contains("Windows PowerShell (Admin)", texts);
        Assert.DoesNotContain("Terminal", texts);
        Assert.Equal(("powershell.exe", (string?)null), QuickLinks.Target(QuickLink.Terminal, machine));
        Assert.Equal(("powershell.exe", "runas"), QuickLinks.Target(QuickLink.TerminalAdmin, machine));
        Assert.Equal(("wt.exe", (string?)null), QuickLinks.Target(QuickLink.Terminal, Desktop));
        Assert.Equal(("wt.exe", "runas"), QuickLinks.Target(QuickLink.TerminalAdmin, Desktop));
    }

    [Fact]
    public void The_power_menu_follows_what_Windows_shows()
    {
        List<string> Power(QuickLinkMachine machine) =>
            [.. QuickLinks.Items(machine).Single(item => item.Children is not null).Children!.Select(item => item.Text)];
        Assert.Equal(["Sign out", "Sleep", "Hibernate", "Shut down", "Restart"], Power(Desktop with { Hibernate = true }));
        Assert.Equal(["Sign out", "Shut down", "Restart"], Power(Desktop with { Sleep = false }));
    }

    [Fact]
    public void Each_line_opens_what_Windows_opens()
    {
        var system = Environment.SystemDirectory;
        Assert.Equal(("ms-settings:appsfeatures", (string?)null), QuickLinks.Target(QuickLink.InstalledApps, Desktop));
        Assert.Equal((Path.Combine(system, "mblctr.exe"), (string?)null), QuickLinks.Target(QuickLink.MobilityCenter, Desktop));
        Assert.Equal(("ms-settings:powersleep", (string?)null), QuickLinks.Target(QuickLink.PowerOptions, Desktop));
        Assert.Equal((Path.Combine(system, "eventvwr.msc"), (string?)null), QuickLinks.Target(QuickLink.EventViewer, Desktop));
        Assert.Equal(("ms-settings:about", (string?)null), QuickLinks.Target(QuickLink.System, Desktop));
        Assert.Equal((Path.Combine(system, "devmgmt.msc"), (string?)null), QuickLinks.Target(QuickLink.DeviceManager, Desktop));
        Assert.Equal(("ms-settings:network", (string?)null), QuickLinks.Target(QuickLink.NetworkConnections, Desktop));
        Assert.Equal((Path.Combine(system, "diskmgmt.msc"), (string?)null), QuickLinks.Target(QuickLink.DiskManagement, Desktop));
        Assert.Equal((Path.Combine(system, "compmgmt.msc"), (string?)null), QuickLinks.Target(QuickLink.ComputerManagement, Desktop));
        Assert.Equal(("taskmgr.exe", (string?)null), QuickLinks.Target(QuickLink.TaskManager, Desktop));
        Assert.Equal(("ms-settings:", (string?)null), QuickLinks.Target(QuickLink.Settings, Desktop));
        Assert.Equal(("explorer.exe", (string?)null), QuickLinks.Target(QuickLink.FileExplorer, Desktop));
    }

    [Theory]
    [InlineData(QuickLink.Search)]
    [InlineData(QuickLink.Run)]
    [InlineData(QuickLink.Desktop)]
    [InlineData(QuickLink.ShutDownOrSignOut)]
    [InlineData(QuickLink.SignOut)]
    [InlineData(QuickLink.Sleep)]
    [InlineData(QuickLink.Hibernate)]
    [InlineData(QuickLink.ShutDown)]
    [InlineData(QuickLink.Restart)]
    public void Keys_power_and_Desktop_open_nothing_themselves(QuickLink link)
    {
        Assert.Null(QuickLinks.Target(link, Desktop));
    }

    [Fact]
    public void The_power_lines_are_the_five_of_the_submenu_and_lock()
    {
        var power = Enum.GetValues<QuickLink>().Where(QuickLinks.IsPower).ToList();
        // quick: Lock joined them for the power widget; the submenu itself still has the five.
        // §641: and Start's Switch user and its update rows.
        Assert.Equal([QuickLink.SignOut, QuickLink.Sleep, QuickLink.Hibernate, QuickLink.ShutDown, QuickLink.Restart, QuickLink.Lock,
            QuickLink.SwitchUser, QuickLink.UpdateRestart, QuickLink.UpdateShutDown], power);
        var machine = new QuickLinkMachine(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: true);
        Assert.DoesNotContain(Lines(QuickLinks.Items(machine)), item => item.Text == "Lock");
    }
}
