// Who a notification came from (NotificationApps): the Start menu, the AppUserModelId registration with
// its indirect strings, a package, the notification area's program, and last a name made from the id —
// over a made-up source, and once over this PC's own registrations, read only.

using Karpo.Dock;
using Microsoft.Win32;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;

namespace Karpo.Tests.Islands;

public sealed class NotificationAppsTests(ITestOutputHelper output)
{
    /// <summary>A made-up Windows: what each place answers, and nothing else.</summary>
    private sealed class Made : INotificationAppSource
    {
        public Dictionary<string, string> Start { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(string, string), string> Registry { get; } = [];
        public Dictionary<string, string> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Logos { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Tray { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Strings { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Descriptions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Pixels> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Pixels> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Asked { get; } = [];

        public string? StartName(string aumid) => Start.GetValueOrDefault(aumid);
        public Pixels? StartIcon(string aumid, int px) => Icons.GetValueOrDefault("start:" + aumid);
        public string? Registered(string aumid, string value) => Registry.GetValueOrDefault((aumid, value));
        public string? PackageName(string aumid) => Packages.GetValueOrDefault(aumid);
        public string? PackageLogo(string aumid) => Logos.GetValueOrDefault(aumid);
        public string? TrayProgram(string key) => Tray.GetValueOrDefault(key);

        public string? Indirect(string source)
        {
            Asked.Add(source);
            return Strings.GetValueOrDefault(source);
        }

        public string? Describe(string program) => Descriptions.GetValueOrDefault(program);
        public Pixels? FileIcon(string path, int px) => Icons.GetValueOrDefault(path);
        public Pixels? Picture(string path, int px) => Files.GetValueOrDefault(path);
    }

    private static Pixels Square(byte b, byte g, byte r, byte a = 255)
    {
        var data = new byte[4 * 4 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            (data[i], data[i + 1], data[i + 2], data[i + 3]) = (b, g, r, a);
        }
        return new Pixels { Data = data, Width = 4, Height = 4 };
    }

    private const string Defender = "Windows.Defender.SecurityCenter";

    /// <summary>
    /// The bug: Windows Security's toasts come as Windows.Defender.SecurityCenter, which the Start menu
    /// doesn't list; its name is a resource string of its registration and its icon a file there.
    /// </summary>
    [Fact]
    public void WindowsSecurityIsNamedAndDrawnFromItsRegistration()
    {
        var shield = Square(0xD4, 0x78, 0x00);
        var made = new Made();
        made.Registry[(Defender, "DisplayName")] = @"@C:\Windows\System32\SecurityHealth\10.0-0\SecurityHealthAgent.dll,-12001";
        made.Registry[(Defender, "IconUri")] = @"C:\WINDOWS\system32\WindowsSecurityIcon.png";
        made.Strings[@"@C:\Windows\System32\SecurityHealth\10.0-0\SecurityHealthAgent.dll,-12001"] = "Windows Security";
        made.Files[@"C:\WINDOWS\system32\WindowsSecurityIcon.png"] = shield;

        var (name, icon) = NotificationApps.Resolve(Defender, made);

        Assert.Equal("Windows Security", name);
        Assert.Same(shield, icon);
        Assert.False(icon!.Mask);
    }

    /// <summary>The Start menu's entry wins, name and icon, and nothing else is asked for a name.</summary>
    [Fact]
    public void TheStartMenuComesFirst()
    {
        const string Mail = "Mail_8wekyb3d8bbwe!App";
        var logo = Square(1, 2, 3);
        var made = new Made();
        made.Start[Mail] = "Outlook";
        made.Icons["start:" + Mail] = logo;
        made.Registry[(Mail, "DisplayName")] = "Not this";

        Assert.Equal(("Outlook", logo), NotificationApps.Resolve(Mail, made));
    }

    /// <summary>A Start menu "name" that is only the id, or a shell path, is no name.</summary>
    [Theory]
    [InlineData("Windows.SystemToast.AutoPlay")]
    [InlineData(@"shell:AppsFolder\Windows.SystemToast.AutoPlay")]
    public void AStartNameThatIsTheIdIsPassedOver(string said)
    {
        const string AutoPlay = "Windows.SystemToast.AutoPlay";
        var made = new Made();
        made.Start[AutoPlay] = said;
        made.Registry[(AutoPlay, "DisplayName")] = @"@C:\WINDOWS\system32\twinui.dll,-9914";
        made.Strings[@"@C:\WINDOWS\system32\twinui.dll,-9914"] = "AutoPlay";

        Assert.Equal("AutoPlay", NotificationApps.Resolve(AutoPlay, made).Name);
    }

    /// <summary>
    /// A registered name: plain words as they are; "@…" resolved; a reference without its "@" taken as
    /// one; one that can't be resolved is no name, and the next place is asked.
    /// </summary>
    [Fact]
    public void RegisteredNamesAreResolvedOrPassedOver()
    {
        var made = new Made();
        made.Strings[@"@C:\WINDOWS\system32\consentux.dll,-103"] = "Device consent";

        Assert.Equal("Windows Update", NotificationApps.Text("Windows Update", made));
        Assert.Equal("Device consent", NotificationApps.Text(@"C:\WINDOWS\system32\consentux.dll,103", made));
        Assert.Null(NotificationApps.Text("@{$(build.PackageMoniker)?ms-resource://SmartActionPlatform/resources/DisplayName}", made));
        Assert.Null(NotificationApps.Text("  ", made));
        Assert.Null(NotificationApps.Text(null, made));

        const string Suggested = "Windows.SystemToast.SuggestedActions";
        made.Registry[(Suggested, "DisplayName")] = "@{$(build.PackageMoniker)?ms-resource://SmartActionPlatform/resources/DisplayName}";
        Assert.Equal("Windows", NotificationApps.Resolve(Suggested, made).Name);
    }

    /// <summary>
    /// A registered icon: a file; an "ms-resource://" of Windows' own, through Windows' index; an
    /// indirect string to a package's file; a file: URI; nothing for a package-relative ms-appx:.
    /// </summary>
    [Fact]
    public void RegisteredIconsAreFound()
    {
        var made = new Made();
        made.Strings["@{Windows?ms-resource://Windows.UI.Shell/Files/Images/LocationIcon.png}"] = @"C:\x\LocationIcon.scale-150.png";
        made.Strings["@{C:\\pq\\resources.pri?ms-resource://PQ/Files/Assets/Square44x44Logo.png}"] = @"C:\pq\Square44x44Logo.targetsize-256.png";

        Assert.Equal(@"C:\x\LocationIcon.scale-150.png", NotificationApps.PicturePath("ms-resource://Windows.UI.Shell/Files/Images/LocationIcon.png", made));
        Assert.Equal(@"C:\pq\Square44x44Logo.targetsize-256.png", NotificationApps.PicturePath("@{C:\\pq\\resources.pri?ms-resource://PQ/Files/Assets/Square44x44Logo.png}", made));
        Assert.Equal(@"C:\a\icon.png", NotificationApps.PicturePath(@"C:\a\icon.png", made));
        Assert.Equal(@"C:\a\icon.png", NotificationApps.PicturePath("file:///C:/a/icon.png", made));
        Assert.Null(NotificationApps.PicturePath("ms-appx:///Assets/icon.png", made));
        Assert.Null(NotificationApps.PicturePath("", made));
        Assert.Null(NotificationApps.PicturePath("ms-resource://Nowhere/x.png", made));
    }

    /// <summary>
    /// A system sender that registered no icon gets the one Windows shows for it, a white glyph, marked
    /// to be drawn in the text colour; a coloured icon is not marked.
    /// </summary>
    [Fact]
    public void WindowsOwnWithoutAnIconGetTheSystemGlyph()
    {
        const string Maintenance = "Windows.SystemToast.SecurityAndMaintenance";
        var glyph = Square(0xFF, 0xFF, 0xFF);
        var made = new Made();
        made.Registry[(Maintenance, "DisplayName")] = @"@C:\WINDOWS\system32\actioncentercpl.dll,-1";
        made.Strings[@"@C:\WINDOWS\system32\actioncentercpl.dll,-1"] = "Security and Maintenance";
        made.Strings[$"@{{Windows?{NotificationApps.DefaultSystemIcon}}}"] = @"C:\x\DefaultSystemNotification.scale-150.png";
        made.Files[@"C:\x\DefaultSystemNotification.scale-150.png"] = glyph;

        var (name, icon) = NotificationApps.Resolve(Maintenance, made);

        Assert.Equal("Security and Maintenance", name);
        Assert.Same(glyph, icon);
        Assert.True(icon!.Mask);
        // An app of no one's: no system glyph.
        Assert.Null(NotificationApps.Resolve("Contoso.Tool", made).Icon);
    }

    /// <summary>
    /// White alone, at any coverage, is a glyph; a colour, or nothing seen, is not. Drawn, a glyph takes
    /// the colour asked for over its own coverage, and anything else is drawn as it is.
    /// </summary>
    [Fact]
    public void AWhiteGlyphIsDrawnInTheTextColour()
    {
        Assert.True(NotificationApps.IsWhiteGlyph(Square(0xFF, 0xFF, 0xFF).Data));
        Assert.True(NotificationApps.IsWhiteGlyph(Square(0x80, 0x80, 0x80, 0x80).Data));
        Assert.False(NotificationApps.IsWhiteGlyph(Square(0xD4, 0x78, 0x00).Data));
        Assert.False(NotificationApps.IsWhiteGlyph(Square(0, 0, 0, 0).Data));
        Assert.False(NotificationApps.IsWhiteGlyph(Square(0x20, 0x20, 0x20).Data));

        var colour = Square(0xD4, 0x78, 0x00);
        Assert.Same(colour, NotificationApps.Drawn(colour, new DXGI_RGBA { r = 0, g = 0, b = 0, a = 1 }));

        var glyph = NotificationApps.Marked(Square(0x80, 0x80, 0x80, 0x80))!;
        var dark = NotificationApps.Drawn(glyph, new DXGI_RGBA { r = 0, g = 0, b = 0, a = 1 });
        Assert.Equal([0, 0, 0, 0x80], dark.Data[..4]);
        Assert.Same(dark, NotificationApps.Drawn(glyph, new DXGI_RGBA { r = 0, g = 0, b = 0, a = 1 }));
        var light = NotificationApps.Drawn(glyph, new DXGI_RGBA { r = 1, g = 1, b = 1, a = 1 });
        Assert.Equal([0x80, 0x80, 0x80, 0x80], light.Data[..4]);
    }

    /// <summary>A package the Start menu doesn't list: its own name and logo.</summary>
    [Fact]
    public void APackageEntryTheStartMenuDoesNotListHasItsOwnName()
    {
        const string Setup = "Microsoft.YourPhone_8wekyb3d8bbwe!YourPhoneSetup";
        var logo = Square(1, 2, 3);
        var made = new Made();
        made.Packages[Setup] = "Phone Link";
        made.Logos[Setup] = @"C:\p\logo.png";
        made.Files[@"C:\p\logo.png"] = logo;

        Assert.Equal(("Phone Link", logo), NotificationApps.Resolve(Setup, made));
        // A desktop app's id is never asked of the packages.
        made.Packages["Contoso.Tool"] = "Wrong";
        Assert.Equal("Tool", NotificationApps.Resolve("Contoso.Tool", made).Name);
    }

    /// <summary>
    /// A balloon of the notification area: its program's description and icon, found by the icon's key
    /// or its GUID; with no program found, Windows'.
    /// </summary>
    [Fact]
    public void ABalloonIsItsTrayProgram()
    {
        var icon = Square(9, 9, 9);
        var made = new Made();
        made.Tray["4627237019786886018"] = @"C:\jb\jetbrains-toolbox.exe";
        made.Tray["{BA82E2DC-F405-47AD-B032-CF0FAA0E3933}"] = @"C:\w\SecurityHealthSystray.exe";
        made.Descriptions[@"C:\jb\jetbrains-toolbox.exe"] = "JetBrains Toolbox";
        made.Icons[@"C:\jb\jetbrains-toolbox.exe"] = icon;

        Assert.Equal(("JetBrains Toolbox", icon), NotificationApps.Resolve("NotifyIconGeneratedAumid_4627237019786886018", made));
        Assert.Equal("SecurityHealthSystray", NotificationApps.Resolve("Microsoft.Explorer.Notification.{ba82e2dc-f405-47ad-b032-cf0faa0e3933}", made).Name);
        Assert.Equal("Windows", NotificationApps.Resolve("NotifyIconGeneratedAumid_11869152862856361901", made).Name);
        Assert.Null(NotificationApps.TrayKey("NotifyIconGeneratedAumid_x1"));
        Assert.Null(NotificationApps.TrayKey("Microsoft.Explorer.Notification.nope"));
    }

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "WindowsCalculator")]
    [InlineData("Windows.SystemToast.SecurityAndMaintenance", "Windows")]
    [InlineData("Windows.Defender.SecurityCenter", "Windows")]
    [InlineData("Microsoft.Windows.InputSwitchToastHandler", "Windows")]
    [InlineData("Microsoft.Explorer.Notification.{0CA331B2-1D9F-FD33-2631-7CA4665C9FE3}", "Windows")]
    [InlineData("Microsoft.Office.OUTLOOK.EXE.15", "Outlook")]
    [InlineData("com.squirrel.Discord.Discord", "Discord")]
    [InlineData("com.squirrel.AnthropicClaude.claude", "Claude")]
    [InlineData("electron.app.Mbed Studio", "Mbed Studio")]
    [InlineData("ThunderbirdToast-D78BF5DD33499EC2", "Thunderbird")]
    [InlineData("3Dconnexion.Local.Notifications.001", "3Dconnexion")]
    [InlineData("Autodesk Installer", "Autodesk Installer")]
    [InlineData("97B593F5E39F7672B486CFEE52751656677D1C3D", "97B593F5E39F7672B486CFEE52751656677D1C3D")]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Tool\tool.exe", "tool")]
    public void AppsNothingNamesAreNamedFromTheirAumid(string aumid, string expected) =>
        Assert.Equal(expected, NotificationText.FallbackName(aumid));

    /// <summary>
    /// This PC, read only: every app Windows keeps notification settings for gets a name that is not
    /// its raw id, wherever one can be found (an id of a hash alone has none to find). The table is
    /// written to the test's output.
    /// </summary>
    [Fact]
    public void EveryAppOnThisPcHasAName()
    {
        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
        var ids = settings?.GetSubKeyNames() ?? [];
        if (ids.Length == 0)
        {
            return;
        }
        var raw = new List<string>();
        var thread = new Thread(() =>
        {
            foreach (var id in ids)
            {
                var (name, icon) = NotificationApps.Resolve(id, NotificationApps.Windows);
                output.WriteLine($"{id} | {name} | {(icon is null ? "no icon" : icon.Mask ? $"glyph {icon.Width}px" : $"{icon.Width}px")}");
                // An id that is words already ("Autodesk Installer") is a name; a hash alone has none to find.
                if (name.Equals(id, StringComparison.OrdinalIgnoreCase) && id.AsSpan().ContainsAny(".!_{\\") && !id.All(char.IsAsciiHexDigit))
                {
                    raw.Add(id);
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Empty(raw);
    }
}
