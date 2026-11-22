// The quick settings in another language: the pages' words, a place in a line, a count picking its
// form, and a text that is read when it is used rather than kept from the language before.

using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class QuickSettingsLanguageTests
{
    private const char NoBreak = ' ';

    [Fact]
    public void TheBatteryPageSaysItsStateAndTimeInGerman()
    {
        var charging = new BatteryState(true, 64, PluggedIn: true, Charging: true, null, TimeSpan.FromMinutes(197), false, null);
        var draining = charging with { PluggedIn = false, Charging = false, Remaining = TimeSpan.FromMinutes(45) };
        using (InLanguage.Use("de"))
        {
            Assert.Equal("Wird geladen", BatteryText.StatusText(charging));
            Assert.Equal("Etwa 3 Std. 17 Min. bis vollständig geladen", BatteryText.TimeText(charging));
            Assert.Equal("Noch etwa 45 Min.", BatteryText.TimeText(draining));
        }
        Assert.Equal("About 3 h 17 min until fully charged", BatteryText.TimeText(charging));
    }

    [Fact]
    public void APercentageInFrenchHasItsNoBreakSpace()
    {
        var headset = new BluetoothDevice("1", "WH-1000XM5", "", Paired: true, Connected: true, Battery: 72);
        using (InLanguage.Use("fr"))
        {
            Assert.Equal($"72{NoBreak}%", BluetoothPage.Level(headset));
            Assert.Equal($"État de la batterie{NoBreak}: 40{NoBreak}% restants", SystemStatus.Describe(new SystemStatus.Battery(40, false, false)).Tip);
        }
        Assert.Equal("72%", BluetoothPage.Level(headset));
    }

    [Fact]
    public void TheConnectedVpnCountPicksItsFormInFrench()
    {
        VpnConnection On(string name) => new(name, "", VpnState.Connected);
        using (InLanguage.Use("fr"))
        {
            Assert.Equal("2 connectés", VpnChoices.Detail([On("Work"), On("Home")]));
        }
        Assert.Equal("2 connected", VpnChoices.Detail([On("Work"), On("Home")]));
    }

    [Fact]
    public void ARestartWaitingIsSaidInSpanish()
    {
        using (InLanguage.Use("es"))
        {
            Assert.Equal("Reinicio necesario", WindowsUpdate.Status(WindowsUpdate.Shown(UpdateState.Due)));
        }
    }

    [Fact]
    public void ANetworkRowsLineIsInChinese()
    {
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("已连接，安全", WifiProfiles.StateText(connected: true, secured: true));
            Assert.Equal("无 Internet，开放", WifiProfiles.StateText(connected: true, secured: false, internet: false));
        }
    }

    [Fact]
    public void TheTitlesAndTheHiddenNetworkAreReadWhenUsedNotKept()
    {
        using (InLanguage.Use("de"))
        {
            Assert.Equal("WLAN", WifiPage.Title);
            Assert.Equal("Ausgeblendetes Netzwerk", WifiProfiles.Hidden(3, WifiSecurity.Wpa2Personal, 4, 7).Ssid);
            Assert.Equal(["Beste Energieeffizienz", "Ausbalanciert", "Beste Leistung"], PowerModePage.Modes.Select(mode => mode.Name));
        }
        Assert.Equal("Wi-Fi", WifiPage.Title);
        Assert.Equal("Hidden network", WifiProfiles.HiddenName);
        Assert.Equal("Balanced", PowerModePage.Modes[1].Name);
    }
}
