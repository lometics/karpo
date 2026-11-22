// A Wi-Fi network's Properties (WifiProperties.cs). Clicking it ended the dock: Windows' dialog was
// asked of wlanapi.dll, which does not export it, and the EntryPointNotFoundException that followed
// reached the top of the dialog's thread. These hold the three things that fix it: the import is
// one Windows has; the dock's side lets nothing out of its thread and opens Windows' Wi-Fi
// settings wherever the dialog was not shown; and the dialog's own process reads its arguments
// back and says how it went in its exit code.
//
// The dialog is never shown here. Its call is handed in, and of the real one only the export is
// looked up — nothing reaches this PC's Wi-Fi or its saved networks.

using System.Reflection;
using System.Runtime.InteropServices;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class WifiPropertiesTests
{
    /// <summary>
    /// Every function the Wi-Fi code imports is exported by the library it names, on this PC. The
    /// dialog's was not (wlanapi.dll has no WlanUIEditProfile; wlanui.dll has), and the first click
    /// found out. Only looked up, never called.
    /// </summary>
    [Fact]
    public void EveryWlanImportIsThere()
    {
        var imports = typeof(WifiProperties).Assembly.GetType("Karpo.Dock.QuickSettings.Services.Wlan", throwOnError: true)!
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(method => (Method: method, Import: method.GetCustomAttribute<DllImportAttribute>()))
            .Where(found => found.Import is not null)
            .ToList();
        Assert.Contains(imports, found => found.Method.Name == "WlanUIEditProfile");
        var missing = new List<string>();
        foreach (var (method, import) in imports)
        {
            var entry = import!.EntryPoint ?? method.Name;
            if (!NativeLibrary.TryLoad(import.Value, typeof(WifiProperties).Assembly, DllImportSearchPath.System32, out var library)
                || !NativeLibrary.TryGetExport(library, entry, out _))
            {
                missing.Add($"{import.Value}!{entry}");
            }
        }
        Assert.Empty(missing);
    }

    /// <summary>
    /// The crash itself: the dialog's call throws as the first one did. It is caught on the thread,
    /// and Windows' Wi-Fi settings open in its place. A test host that ended here instead of
    /// reporting would be this failing.
    /// </summary>
    [Fact]
    public void ADialogThatThrowsOpensWiFiSettings()
    {
        var opened = new List<string>();
        var thread = WifiProperties.Begin(
            "Rabbit Hole",
            Guid.NewGuid(),
            static (_, _) => throw new EntryPointNotFoundException("A test's dialog throws on purpose (WifiPropertiesTests)"),
            opened.Add);
        Assert.NotNull(thread);
        Assert.True(thread.Join(Patience.Ceiling), "the properties thread never ended");
        Assert.Equal([WifiProperties.Settings], opened);
        Assert.Equal("ms-settings:network-wifi", WifiProperties.Settings);
    }

    /// <summary>
    /// Nothing at all gets out of the thread: the dialog throws, and opening the settings in its
    /// place throws too.
    /// </summary>
    [Fact]
    public void NothingEscapesTheThread()
    {
        var asked = 0;
        var thread = WifiProperties.Begin(
            "Rabbit Hole",
            Guid.NewGuid(),
            static (_, _) => throw new AccessViolationException("A test's dialog throws on purpose (WifiPropertiesTests)"),
            _ =>
            {
                Interlocked.Increment(ref asked);
                throw new InvalidOperationException("A test's settings throw on purpose (WifiPropertiesTests)");
            });
        Assert.NotNull(thread);
        Assert.True(thread.Join(Patience.Ceiling), "the properties thread never ended");
        Assert.Equal(1, asked);
    }

    /// <summary>A dialog shown opens nothing else; one Windows would not show opens the settings.</summary>
    [Fact]
    public void OnlyADialogNotShownOpensTheSettings()
    {
        var opened = new List<string>();
        string? asked = null;
        WifiProperties.Show("Rabbit Hole", Guid.Empty, (profile, _) => (asked = profile) is not null, opened.Add);
        Assert.Equal("Rabbit Hole", asked);
        Assert.Empty(opened);

        WifiProperties.Show("Rabbit Hole", Guid.Empty, static (_, _) => false, opened.Add);
        Assert.Equal([WifiProperties.Settings], opened);
    }

    /// <summary>
    /// The dialog's process reads back exactly what the dock handed it, a name with spaces, quotes
    /// and a switch's dashes included (Process.Start quotes each argument whole).
    /// </summary>
    [Theory]
    [InlineData("Rabbit Hole")]
    [InlineData("Café \"5 GHz\"")]
    [InlineData("--smoke")]
    [InlineData(@"C:\ trailing\")]
    public void TheDialogsProcessReadsItsArgumentsBack(string name)
    {
        var adapter = Guid.NewGuid();
        (string Profile, Guid Adapter)? asked = null;
        var code = WifiProperties.Run(WifiProperties.Arguments(name, adapter), (profile, id) =>
        {
            asked = (profile, id);
            return 0;
        });
        Assert.Equal(WifiProperties.Shown, code);
        Assert.Equal((name, adapter), asked);

    }

    /// <summary>The dialog's process says how it went: shown, refused by Windows, or failed.</summary>
    [Fact]
    public void TheDialogsProcessSaysHowItWent()
    {
        var args = WifiProperties.Arguments("Rabbit Hole", Guid.NewGuid());
        // ERROR_NOT_FOUND: no such profile; ERROR_INVALID_PARAMETER: no such adapter.
        Assert.Equal(WifiProperties.Refused, WifiProperties.Run(args, static (_, _) => 1168));
        Assert.Equal(WifiProperties.Refused, WifiProperties.Run(args, static (_, _) => 87));
        // Closed with OK, Cancel or its close button: ERROR_SUCCESS, as Windows answers all three.
        // A cancel answered as ERROR_CANCELLED is a cancel too. Neither opens the settings.
        Assert.Equal(WifiProperties.Shown, WifiProperties.Run(args, static (_, _) => 0));
        Assert.Equal(WifiProperties.Shown, WifiProperties.Run(args, static (_, _) => 1223));
        Assert.Equal(WifiProperties.Failed, WifiProperties.Run(args, static (_, _) => throw new EntryPointNotFoundException("A test's dialog throws on purpose (WifiPropertiesTests)")));

        // Wrong arguments show nothing at all.
        var called = false;
        Func<string, Guid, uint> dialog = (_, _) =>
        {
            called = true;
            return 0;
        };
        Assert.Equal(WifiProperties.Failed, WifiProperties.Run([WifiProperties.Switch], dialog));
        Assert.Equal(WifiProperties.Failed, WifiProperties.Run([WifiProperties.Switch, "not a guid", "Rabbit Hole"], dialog));
        Assert.Equal(WifiProperties.Failed, WifiProperties.Run([WifiProperties.Switch, Guid.NewGuid().ToString(), ""], dialog));
        Assert.Equal(WifiProperties.Failed, WifiProperties.Run([WifiProperties.Switch, Guid.NewGuid().ToString(), "a", "b"], dialog));
        Assert.False(called);
    }

    /// <summary>A network Windows has no profile for has no dialog to show: the page opens the settings itself.</summary>
    [Fact]
    public void ANetworkWithoutAProfileHasNoDialog()
    {
        using var wifi = new WindowsWifi();
        var network = new WifiNetwork("Rabbit Hole", "Rabbit Hole"u8.ToArray(), 4, WifiSecurity.Wpa2Personal, 4, Connected: true, Profile: null);
        Assert.False(wifi.ShowProperties(network));
        // Not started, so it has no adapter either: nothing is asked of Windows.
        Assert.False(wifi.ShowProperties(network with { Profile = "Rabbit Hole" }));
    }
}
