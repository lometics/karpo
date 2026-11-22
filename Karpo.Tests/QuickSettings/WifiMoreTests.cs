// What the Wi-Fi page joins beyond a network with a key, as Windows' own list does: a hidden
// network by the name the person types, and an enterprise (802.1X) network with a user name and a
// password (WifiProfiles, Wifi.cs). Held through the Wi-Fi seams alone — the profiles and the
// credentials Windows would be handed, read back as XML, and the made-up networks' answers — so
// nothing here touches this PC's Wi-Fi.

using System.Text;
using System.Xml.Linq;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class WifiMoreTests
{
    private static readonly XNamespace Wlan = "http://www.microsoft.com/networking/WLAN/profile/v1";

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static XElement Profile(string xml) => XDocument.Parse(xml).Root!;

    // --- A hidden network ----------------------------------------------------------------------

    /// <summary>
    /// The networks that say no name are one entry, "Hidden network", with no SSID of its own; named,
    /// it is the name's bytes, and the rest of it — its security, its signal — stays.
    /// </summary>
    [Fact]
    public void TheHiddenNetworkIsOneEntryAndTakesTheNameTyped()
    {
        var hidden = WifiProfiles.Hidden(3, WifiSecurity.Wpa2Personal, 4, 7);
        Assert.True(hidden.Hidden);
        Assert.Equal(WifiProfiles.HiddenName, hidden.Ssid);
        Assert.Empty(hidden.SsidBytes);
        Assert.Null(hidden.Profile);

        var named = WifiProfiles.Named(hidden, "Café");
        Assert.True(named.Hidden);
        Assert.Equal("Café", named.Ssid);
        Assert.Equal(Bytes("Café"), named.SsidBytes);
        Assert.Equal((3, WifiSecurity.Wpa2Personal, 7), (named.Bars, named.Security, named.Auth));
    }

    /// <summary>A name is 1 to 32 bytes, as an SSID is: none, or one longer, says what is wrong.</summary>
    [Fact]
    public void AHiddenNetworksNameIsOneToThirtyTwoBytes()
    {
        Assert.NotNull(WifiProfiles.NameProblem(""));
        Assert.Null(WifiProfiles.NameProblem(new string('a', 32)));
        Assert.NotNull(WifiProfiles.NameProblem(new string('a', 33)));
        // Sixteen two-byte letters are 32 bytes; seventeen are too many.
        Assert.Null(WifiProfiles.NameProblem(new string('é', 16)));
        Assert.NotNull(WifiProfiles.NameProblem(new string('é', 17)));
    }

    /// <summary>A hidden network's profile says it is not broadcast, so Windows looks for it by name; any other does not.</summary>
    [Fact]
    public void AHiddenNetworksProfileSaysItIsNotBroadcast()
    {
        string? NonBroadcast(bool hidden) => Profile(WifiProfiles.Xml(Bytes("Burrow"), "Burrow", WifiSecurity.Wpa2Personal, 4, "carrots!", automatic: true, hidden))
            .Element(Wlan + "SSIDConfig")!.Element(Wlan + "nonBroadcast")?.Value;

        Assert.Equal("true", NonBroadcast(hidden: true));
        Assert.Null(NonBroadcast(hidden: false));
        Assert.Equal("true", Profile(WifiProfiles.EnterpriseXml(Bytes("Burrow"), "Burrow", 6, 4, automatic: false, hidden: true))
            .Element(Wlan + "SSIDConfig")!.Element(Wlan + "nonBroadcast")?.Value);
    }

    // --- An enterprise network -----------------------------------------------------------------

    /// <summary>WPA, WPA2 and WPA3-Enterprise sign in with a password; the 192-bit one only with a certificate.</summary>
    [Theory]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(11, true)]
    [InlineData(8, false)]
    public void AnEnterpriseNetworkSignsInWithAPasswordWhereWindowsCan(int auth, bool password)
    {
        Assert.Equal(password, WifiProfiles.SignsInWithPassword(auth));
        Assert.Equal(password, WifiProfiles.CanJoin(WifiSecurity.Enterprise, auth));
    }

    /// <summary>
    /// The profile is the sign-in Windows' list sets up for such a network: 802.1X by the user's own
    /// credentials, PEAP (25) with MSCHAPv2 (26) inside it, no shared key, and the server's
    /// certificate checked and shown to the person to trust — never taken on faith.
    /// </summary>
    [Theory]
    [InlineData(3, 4, "WPA", "AES")]
    [InlineData(3, 2, "WPA", "TKIP")]
    [InlineData(6, 4, "WPA2", "AES")]
    [InlineData(11, 4, "WPA3ENT", "AES")]
    public void TheEnterpriseProfileIsPeapWithMsChapV2(int auth, int cipher, string authentication, string encryption)
    {
        var root = Profile(WifiProfiles.EnterpriseXml(Bytes("eduroam"), "eduroam", auth, cipher, automatic: true));
        var security = root.Element(Wlan + "MSM")!.Element(Wlan + "security")!;
        var encrypted = security.Element(Wlan + "authEncryption")!;
        Assert.Equal(authentication, encrypted.Element(Wlan + "authentication")!.Value);
        Assert.Equal(encryption, encrypted.Element(Wlan + "encryption")!.Value);
        Assert.Equal("true", encrypted.Element(Wlan + "useOneX")!.Value);
        Assert.Null(security.Element(Wlan + "sharedKey"));
        Assert.Equal("auto", root.Element(Wlan + "connectionMode")!.Value);

        XNamespace oneX = "http://www.microsoft.com/networking/OneX/v1";
        var sign = security.Element(oneX + "OneX")!;
        Assert.Equal("user", sign.Element(oneX + "authMode")!.Value);
        var types = sign.Descendants().Where(element => element.Name.LocalName == "Type").Select(element => element.Value).ToList();
        Assert.Equal(["25", "25", "26"], types);
        Assert.Equal("false", sign.Descendants().Single(element => element.Name.LocalName == "DisableUserPromptForServerValidation").Value);
        Assert.Equal("true", sign.Descendants().Single(element => element.Name.LocalName == "PerformServerValidation").Value);
        Assert.Equal("false", sign.Descendants().Single(element => element.Name.LocalName == "UseWinLogonCredentials").Value);
    }

    /// <summary>
    /// The credentials are the user name, the password and the domain where the name is written
    /// "DOMAIN\user" — escaped, so a password with XML's own characters is the password it was.
    /// </summary>
    [Fact]
    public void TheCredentialsCarryTheUserTheDomainAndThePasswordWhole()
    {
        static (string User, string Password, string Domain) Read(WifiSignIn signIn)
        {
            XNamespace chap = "http://www.microsoft.com/provisioning/MsChapV2UserPropertiesV1";
            var root = XDocument.Parse(WifiProfiles.EapUserXml(signIn)).Root!;
            Assert.Equal("EapHostUserCredentials", root.Name.LocalName);
            return (root.Descendants(chap + "Username").Single().Value, root.Descendants(chap + "Password").Single().Value, root.Descendants(chap + "LogonDomain").Single().Value);
        }

        Assert.Equal(("alice@example.edu", "wonderland", ""), Read(new WifiSignIn(" alice@example.edu ", "wonderland")));
        Assert.Equal(("alice", "a<b&c>\"d'", "CONTOSO"), Read(new WifiSignIn(@"CONTOSO\alice", "a<b&c>\"d'")));
    }

    /// <summary>Both are asked for: a user name that is only spaces, or no password, says which is missing.</summary>
    [Fact]
    public void ASignInNeedsAUserNameAndAPassword()
    {
        Assert.NotNull(WifiProfiles.SignInProblem(new WifiSignIn("  ", "secret")));
        Assert.NotNull(WifiProfiles.SignInProblem(new WifiSignIn("alice", "")));
        Assert.Null(WifiProfiles.SignInProblem(new WifiSignIn("alice", " ")));
    }

    /// <summary>
    /// How a sign-in that did not connect ends: refused credentials that were typed are wrong and asked
    /// again; refused without any, they are asked for; anything else is a failure, and one after
    /// credentials that were typed sends the person to Windows' list, which signs in in more ways.
    /// </summary>
    [Fact]
    public void ASignInThatDidNotConnectSaysWhy()
    {
        Assert.Equal(WifiOutcome.WrongSignIn, WifiProfiles.SignInEnded("n", security: true, withSignIn: true).Outcome);
        Assert.Equal(WifiOutcome.NeedsSignIn, WifiProfiles.SignInEnded("n", security: true, withSignIn: false).Outcome);
        var failed = WifiProfiles.SignInEnded("n", security: false, withSignIn: true);
        Assert.Equal((WifiOutcome.Failed, WifiProfiles.CantJoin), (failed.Outcome, failed.Problem));
        Assert.Equal(WifiOutcome.Failed, WifiProfiles.SignInEnded("n", security: false, withSignIn: false).Outcome);
    }

    // --- The seam ------------------------------------------------------------------------------

    /// <summary>
    /// The made-up networks answer as Windows does: a hidden network by a name nobody answers to
    /// fails, by its own asks for its key and joins with it; an enterprise network asks for a sign-in,
    /// refuses a wrong one and joins with the right one; a certificate-only one is left to Windows'
    /// list. Properties open only for a network with a profile, whose own they are.
    /// </summary>
    [Fact]
    public void TheSamplesAnswerAsWindowsDoes()
    {
        var wifi = new SampleWifi { Hidden = true };
        var hidden = Assert.Single(wifi.Read().Networks, network => network.Hidden);

        wifi.Connect(WifiProfiles.Named(hidden, "Nobody"), null, automatic: true);
        Assert.Equal(WifiOutcome.Failed, wifi.Read().Attempt?.Outcome);
        var burrow = WifiProfiles.Named(hidden, SampleWifi.HiddenSsid);
        wifi.Connect(burrow, null, automatic: true);
        Assert.Equal(WifiOutcome.NeedsKey, wifi.Read().Attempt?.Outcome);
        wifi.Connect(burrow, "carrots!", automatic: true);
        Assert.Equal(WifiOutcome.Connected, wifi.Read().Attempt?.Outcome);
        Assert.Contains(wifi.Read().Networks, network => network.Ssid == SampleWifi.HiddenSsid && network.Connected && !network.Hidden);

        var eduroam = wifi.Read().Networks.Single(network => network.Ssid == "eduroam");
        wifi.Connect(eduroam, null, automatic: true);
        Assert.Equal(WifiOutcome.NeedsSignIn, wifi.Read().Attempt?.Outcome);
        wifi.Connect(eduroam, null, automatic: true, new WifiSignIn("alice", "rabbit"));
        Assert.Equal(WifiOutcome.WrongSignIn, wifi.Read().Attempt?.Outcome);
        wifi.Connect(eduroam, null, automatic: true, new WifiSignIn("alice", "wonderland"));
        Assert.Equal(WifiOutcome.Connected, wifi.Read().Attempt?.Outcome);

        wifi.Connect(eduroam with { Auth = 8 }, null, automatic: true);
        Assert.Equal((WifiOutcome.Failed, WifiProfiles.CantJoin), (wifi.Read().Attempt!.Outcome, wifi.Read().Attempt!.Problem));

        Assert.False(wifi.ShowProperties(eduroam));
        Assert.True(wifi.ShowProperties(wifi.Read().Networks.Single(network => network.Ssid == "Rabbit Hole")));
        Assert.Equal(["Rabbit Hole"], wifi.PropertiesShown);
    }
}
