// What Windows needs to join a network it has no profile for: a WLAN profile
// in XML (the schema of WlanSetProfile, as `netsh wlan export profile` writes
// it), made from the network's name and the key the person typed. The SSID goes
// in as hex, which is exact whatever its bytes are, and as a name for people;
// the name and the key are escaped for XML, and anything XML can't carry is
// left out of the name. Keys are checked first against what each kind of
// network accepts, so a key Windows would refuse gets a clear message instead.
//
// A hidden network's profile says so (nonBroadcast), so Windows looks for it by
// name. An enterprise network's is the sign-in Windows' list sets up for one
// that takes a user name and a password — PEAP, with MSCHAPv2 inside it — with
// the server's certificate left for Windows to show and the person to trust;
// the user name and the password go in as the profile's credentials
// (EapHostUserCredentials), escaped like everything else.

using System.Globalization;
using System.Text;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>How a network is secured, as far as joining it goes.</summary>
internal enum WifiSecurity
{
    Open,

    /// <summary>Enhanced open (OWE): encrypted, no key.</summary>
    Owe,

    Wep,

    /// <summary>WPA-Personal (TKIP).</summary>
    WpaPersonal,

    /// <summary>WPA2-Personal.</summary>
    Wpa2Personal,

    /// <summary>WPA3-Personal (SAE).</summary>
    Wpa3Personal,

    /// <summary>802.1X: an account and a password, or a certificate. Karpo signs in with the first; a certificate is Windows' list's.</summary>
    Enterprise,
}

internal static class WifiProfiles
{
    /// <summary>What Windows' list calls the entry for the networks that do not say their name.</summary>
    public static string HiddenName => S.DockQuickWifiHidden;

    /// <summary>What the page says of a network Karpo can't sign in to.</summary>
    public static string CantJoin => S.DockQuickWifiCantJoin;

    /// <summary>The list's entry for the networks in range that do not say their name, with the strongest one's signal and security.</summary>
    public static WifiNetwork Hidden(int bars, WifiSecurity security, int cipher, int auth) =>
        new(HiddenName, [], bars, security, cipher, Connected: false, Profile: null, Hidden: true, Auth: auth);

    /// <summary>The hidden network <paramref name="hidden"/> under the name <paramref name="name"/> the person typed for it.</summary>
    public static WifiNetwork Named(WifiNetwork hidden, string name) =>
        hidden with { Ssid = name, SsidBytes = Encoding.UTF8.GetBytes(name) };

    /// <summary>What is wrong with a hidden network's name as typed, or null: an SSID is 1 to 32 bytes.</summary>
    public static string? NameProblem(string name) =>
        name.Length == 0 ? S.DockQuickWifiNameEmpty
        : Encoding.UTF8.GetByteCount(name) > 32 ? S.DockQuickWifiNameLong
        : null;

    /// <summary>
    /// Whether an enterprise network of authentication <paramref name="auth"/> (DOT11_AUTH_ALGORITHM)
    /// signs in with a user name and a password Windows can use: WPA, WPA2 and WPA3-Enterprise. The
    /// 192-bit WPA3-Enterprise takes only a certificate.
    /// </summary>
    public static bool SignsInWithPassword(int auth) => auth is 3 or 6 or 11;

    /// <summary>What is missing from <paramref name="signIn"/>, or null: both a user name and a password.</summary>
    public static string? SignInProblem(WifiSignIn signIn) =>
        signIn.User.Trim().Length == 0 ? S.DockQuickWifiUserEmpty
        : signIn.Password.Length == 0 ? S.DockQuickWifiPasswordEmpty
        : null;

    /// <summary>How an enterprise sign-in that did not connect ended, from whether Windows said the security failed and whether credentials were typed.</summary>
    public static WifiAttempt SignInEnded(string ssid, bool security, bool withSignIn) =>
        security
            ? withSignIn
                ? new WifiAttempt(ssid, WifiOutcome.WrongSignIn, S.DockQuickWifiWrongSignIn)
                : new WifiAttempt(ssid, WifiOutcome.NeedsSignIn)
            : new WifiAttempt(ssid, WifiOutcome.Failed, withSignIn ? CantJoin : S.DockQuickWifiCantConnect);

    /// <summary>
    /// The kind of security for a network's default algorithms (DOT11_AUTH_ALGORITHM and
    /// DOT11_CIPHER_ALGORITHM, as WLAN_AVAILABLE_NETWORK reports them).
    /// </summary>
    public static WifiSecurity SecurityOf(int authAlgorithm, int cipherAlgorithm) => authAlgorithm switch
    {
        // Open, or shared key: WEP when there is a WEP cipher, else nothing.
        1 or 2 => cipherAlgorithm is 1 or 5 or 0x101 ? WifiSecurity.Wep : WifiSecurity.Open,
        4 => WifiSecurity.WpaPersonal,
        7 => WifiSecurity.Wpa2Personal,
        9 => WifiSecurity.Wpa3Personal,
        10 => WifiSecurity.Owe,
        _ => WifiSecurity.Enterprise,
    };

    /// <summary>Whether joining needs a key typed in.</summary>
    public static bool NeedsKey(WifiSecurity security) =>
        security is WifiSecurity.Wep or WifiSecurity.WpaPersonal or WifiSecurity.Wpa2Personal or WifiSecurity.Wpa3Personal;

    /// <summary>
    /// Whether Karpo can join it: every network but an enterprise one that signs in with a
    /// certificate alone (<see cref="SignsInWithPassword"/>).
    /// </summary>
    public static bool CanJoin(WifiSecurity security, int auth) => security != WifiSecurity.Enterprise || SignsInWithPassword(auth);

    /// <summary>
    /// How joining <paramref name="network"/> ends before Windows is asked anything, where that is
    /// plain from the network and what was typed: it can't be joined, a key or a sign-in is asked
    /// for, or what was typed is wrong. Null where Windows must say. <paramref name="hasProfile"/>
    /// is whether Windows has a profile to join it with; null where that is not known yet (a hidden
    /// network's, which is looked up by the name typed), and the answers that hang on it are Windows'.
    /// </summary>
    public static WifiAttempt? Foreseen(WifiNetwork network, string? key, WifiSignIn? signIn, bool? hasProfile)
    {
        if (!CanJoin(network.Security, network.Auth))
        {
            return new WifiAttempt(network.Ssid, WifiOutcome.Failed, CantJoin);
        }
        if (network.Security == WifiSecurity.Enterprise)
        {
            if (signIn is not null)
            {
                return SignInProblem(signIn) is { } missing ? new WifiAttempt(network.Ssid, WifiOutcome.WrongSignIn, missing) : null;
            }
            // No profile, so no credentials Windows saved: they are asked for.
            return hasProfile == false ? new WifiAttempt(network.Ssid, WifiOutcome.NeedsSignIn) : null;
        }
        if (key is not null)
        {
            return KeyProblem(network.Security, key) is { } wrong ? new WifiAttempt(network.Ssid, WifiOutcome.WrongKey, wrong) : null;
        }
        return hasProfile == false && NeedsKey(network.Security) ? new WifiAttempt(network.Ssid, WifiOutcome.NeedsKey) : null;
    }

    /// <summary>What is wrong with <paramref name="key"/> for <paramref name="security"/>, or null when Windows would take it.</summary>
    public static string? KeyProblem(WifiSecurity security, string key)
    {
        switch (security)
        {
            case WifiSecurity.Wep:
                return key.Length is 5 or 13 && IsPrintableAscii(key) || key.Length is 10 or 26 && IsHex(key)
                    ? null
                    : S.DockQuickWifiWepKey;
            case WifiSecurity.WpaPersonal:
            case WifiSecurity.Wpa2Personal:
            case WifiSecurity.Wpa3Personal:
                if (key.Length == 64 && IsHex(key))
                {
                    return null;
                }
                if (key.Length is < 8 or > 63)
                {
                    return S.DockQuickWifiKeyLength;
                }
                return IsPrintableAscii(key) ? null : S.DockQuickWifiKeyCharacters;
            default:
                return null;
        }
    }

    /// <summary>
    /// The profile for joining <paramref name="ssid"/> (its raw bytes) with <paramref name="key"/> (null for a
    /// network without one). <paramref name="name"/> is how people read the SSID; <paramref name="cipher"/> the
    /// network's default cipher (TKIP or AES). <paramref name="automatic"/> joins it again by itself later.
    /// </summary>
    public static string Xml(ReadOnlySpan<byte> ssid, string name, WifiSecurity security, int cipher, string? key, bool automatic, bool hidden = false)
    {
        var shown = Escape(ProfileName(ssid, name));
        var (authentication, encryption) = security switch
        {
            WifiSecurity.Owe => ("OWE", "AES"),
            WifiSecurity.Wep => ("open", "WEP"),
            WifiSecurity.WpaPersonal => ("WPAPSK", cipher == 2 ? "TKIP" : "AES"),
            WifiSecurity.Wpa2Personal => ("WPA2PSK", cipher == 2 ? "TKIP" : "AES"),
            WifiSecurity.Wpa3Personal => ("WPA3SAE", "AES"),
            _ => ("open", "none"),
        };
        var xml = new StringBuilder();
        Head(xml, ssid, shown, automatic, hidden);
        xml.Append("\t<MSM>\n\t\t<security>\n\t\t\t<authEncryption>\n");
        xml.Append("\t\t\t\t<authentication>").Append(authentication).Append("</authentication>\n");
        xml.Append("\t\t\t\t<encryption>").Append(encryption).Append("</encryption>\n");
        xml.Append("\t\t\t\t<useOneX>false</useOneX>\n");
        xml.Append("\t\t\t</authEncryption>\n");
        if (key is not null && NeedsKey(security))
        {
            // A 64-digit hex key (or a WEP key in hex) is the key itself; anything else a passphrase.
            var raw = security == WifiSecurity.Wep || key.Length == 64 && IsHex(key);
            xml.Append("\t\t\t<sharedKey>\n");
            xml.Append("\t\t\t\t<keyType>").Append(raw ? "networkKey" : "passPhrase").Append("</keyType>\n");
            xml.Append("\t\t\t\t<protected>false</protected>\n");
            xml.Append("\t\t\t\t<keyMaterial>").Append(Escape(key)).Append("</keyMaterial>\n");
            xml.Append("\t\t\t</sharedKey>\n");
        }
        xml.Append("\t\t</security>\n\t</MSM>\n");
        xml.Append("</WLANProfile>\n");
        return xml.ToString();
    }

    /// <summary>
    /// The profile's start, to its connection mode: its name, the SSID in hex and by name, whether it
    /// is a network that does not say its name, and whether Windows joins it by itself.
    /// </summary>
    private static void Head(StringBuilder xml, ReadOnlySpan<byte> ssid, string shown, bool automatic, bool hidden)
    {
        xml.Append("<?xml version=\"1.0\"?>\n");
        xml.Append("<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\n");
        xml.Append("\t<name>").Append(shown).Append("</name>\n");
        xml.Append("\t<SSIDConfig>\n\t\t<SSID>\n");
        xml.Append("\t\t\t<hex>").Append(Convert.ToHexString(ssid)).Append("</hex>\n");
        xml.Append("\t\t\t<name>").Append(shown).Append("</name>\n");
        xml.Append("\t\t</SSID>\n");
        if (hidden)
        {
            xml.Append("\t\t<nonBroadcast>true</nonBroadcast>\n");
        }
        xml.Append("\t</SSIDConfig>\n");
        xml.Append("\t<connectionType>ESS</connectionType>\n");
        xml.Append("\t<connectionMode>").Append(automatic ? "auto" : "manual").Append("</connectionMode>\n");
    }

    /// <summary>
    /// The profile for signing in to the enterprise network <paramref name="ssid"/> with a user name
    /// and a password: its authentication for <paramref name="auth"/> (WPA, WPA2 or WPA3-Enterprise),
    /// 802.1X by the user's own credentials, and PEAP with MSCHAPv2 inside it — the server's
    /// certificate checked, and shown to the person to trust where Windows does not know it.
    /// </summary>
    public static string EnterpriseXml(ReadOnlySpan<byte> ssid, string name, int auth, int cipher, bool automatic, bool hidden = false)
    {
        var (authentication, encryption) = auth switch
        {
            3 => ("WPA", cipher == 2 ? "TKIP" : "AES"),
            11 => ("WPA3ENT", "AES"),
            _ => ("WPA2", cipher == 2 ? "TKIP" : "AES"),
        };
        var xml = new StringBuilder();
        Head(xml, ssid, Escape(ProfileName(ssid, name)), automatic, hidden);
        xml.Append("\t<MSM>\n\t\t<security>\n\t\t\t<authEncryption>\n");
        xml.Append("\t\t\t\t<authentication>").Append(authentication).Append("</authentication>\n");
        xml.Append("\t\t\t\t<encryption>").Append(encryption).Append("</encryption>\n");
        xml.Append("\t\t\t\t<useOneX>true</useOneX>\n");
        xml.Append("\t\t\t</authEncryption>\n");
        xml.Append("""
                    <OneX xmlns="http://www.microsoft.com/networking/OneX/v1">
                        <authMode>user</authMode>
                        <EAPConfig>
                            <EapHostConfig xmlns="http://www.microsoft.com/provisioning/EapHostConfig">
                                <EapMethod>
                                    <Type xmlns="http://www.microsoft.com/provisioning/EapCommon">25</Type>
                                    <VendorId xmlns="http://www.microsoft.com/provisioning/EapCommon">0</VendorId>
                                    <VendorType xmlns="http://www.microsoft.com/provisioning/EapCommon">0</VendorType>
                                    <AuthorId xmlns="http://www.microsoft.com/provisioning/EapCommon">0</AuthorId>
                                </EapMethod>
                                <Config xmlns="http://www.microsoft.com/provisioning/EapHostConfig">
                                    <Eap xmlns="http://www.microsoft.com/provisioning/BaseEapConnectionPropertiesV1">
                                        <Type>25</Type>
                                        <EapType xmlns="http://www.microsoft.com/provisioning/MsPeapConnectionPropertiesV1">
                                            <ServerValidation>
                                                <DisableUserPromptForServerValidation>false</DisableUserPromptForServerValidation>
                                                <ServerNames></ServerNames>
                                            </ServerValidation>
                                            <FastReconnect>true</FastReconnect>
                                            <InnerEapOptional>false</InnerEapOptional>
                                            <Eap xmlns="http://www.microsoft.com/provisioning/BaseEapConnectionPropertiesV1">
                                                <Type>26</Type>
                                                <EapType xmlns="http://www.microsoft.com/provisioning/MsChapV2ConnectionPropertiesV1">
                                                    <UseWinLogonCredentials>false</UseWinLogonCredentials>
                                                </EapType>
                                            </Eap>
                                            <EnableQuarantineChecks>false</EnableQuarantineChecks>
                                            <RequireCryptoBinding>false</RequireCryptoBinding>
                                            <PeapExtensions>
                                                <PerformServerValidation xmlns="http://www.microsoft.com/provisioning/MsPeapConnectionPropertiesV2">true</PerformServerValidation>
                                                <AcceptServerName xmlns="http://www.microsoft.com/provisioning/MsPeapConnectionPropertiesV2">false</AcceptServerName>
                                            </PeapExtensions>
                                        </EapType>
                                    </Eap>
                                </Config>
                            </EapHostConfig>
                        </EAPConfig>
                    </OneX>

        """);
        xml.Append("\t\t</security>\n\t</MSM>\n");
        xml.Append("</WLANProfile>\n");
        return xml.ToString();
    }

    /// <summary>
    /// The credentials of an enterprise profile (<see cref="EnterpriseXml"/>) for <paramref name="signIn"/>:
    /// PEAP around MSCHAPv2, its user name, its password, and the domain where the user name is
    /// written "DOMAIN\user".
    /// </summary>
    public static string EapUserXml(WifiSignIn signIn)
    {
        var user = signIn.User.Trim();
        var cut = user.IndexOf('\\');
        var domain = cut > 0 ? user[..cut] : "";
        var account = cut > 0 ? user[(cut + 1)..] : user;
        var xml = new StringBuilder();
        xml.Append("<EapHostUserCredentials xmlns=\"http://www.microsoft.com/provisioning/EapHostUserCredentials\" xmlns:eapCommon=\"http://www.microsoft.com/provisioning/EapCommon\" xmlns:baseEap=\"http://www.microsoft.com/provisioning/BaseEapMethodUserCredentials\">\n");
        xml.Append("\t<EapMethod>\n\t\t<eapCommon:Type>25</eapCommon:Type>\n\t\t<eapCommon:AuthorId>0</eapCommon:AuthorId>\n\t</EapMethod>\n");
        xml.Append("\t<Credentials xmlns:eapUser=\"http://www.microsoft.com/provisioning/EapUserPropertiesV1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:baseEap=\"http://www.microsoft.com/provisioning/BaseEapUserPropertiesV1\" xmlns:MsPeap=\"http://www.microsoft.com/provisioning/MsPeapUserPropertiesV1\" xmlns:MsChapV2=\"http://www.microsoft.com/provisioning/MsChapV2UserPropertiesV1\">\n");
        xml.Append("\t\t<baseEap:Eap>\n\t\t\t<baseEap:Type>25</baseEap:Type>\n\t\t\t<MsPeap:EapType>\n");
        xml.Append("\t\t\t\t<baseEap:Eap>\n\t\t\t\t\t<baseEap:Type>26</baseEap:Type>\n\t\t\t\t\t<MsChapV2:EapType>\n");
        xml.Append("\t\t\t\t\t\t<MsChapV2:Username>").Append(Escape(account)).Append("</MsChapV2:Username>\n");
        xml.Append("\t\t\t\t\t\t<MsChapV2:Password>").Append(Escape(signIn.Password)).Append("</MsChapV2:Password>\n");
        xml.Append("\t\t\t\t\t\t<MsChapV2:LogonDomain>").Append(Escape(domain)).Append("</MsChapV2:LogonDomain>\n");
        xml.Append("\t\t\t\t\t</MsChapV2:EapType>\n\t\t\t\t</baseEap:Eap>\n");
        xml.Append("\t\t\t</MsPeap:EapType>\n\t\t</baseEap:Eap>\n\t</Credentials>\n");
        xml.Append("</EapHostUserCredentials>\n");
        return xml.ToString();
    }

    /// <summary>
    /// The profile's name, which Windows lists it by: the network's name without what XML can't
    /// carry, or its SSID in hex when nothing of the name is left.
    /// </summary>
    public static string ProfileName(ReadOnlySpan<byte> ssid, string name) =>
        Clean(name) is { Length: > 0 } clean && !string.IsNullOrWhiteSpace(clean) ? clean : Convert.ToHexString(ssid);

    /// <summary>Text for an XML element: <see cref="Clean"/>, and the five special characters escaped.</summary>
    public static string Escape(string text)
    {
        var clean = Clean(text);
        var escaped = new StringBuilder(clean.Length + 8);
        foreach (var c in clean)
        {
            escaped.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => c.ToString(),
            });
        }
        return escaped.ToString();
    }

    /// <summary><paramref name="text"/> without what XML 1.0 can't carry at all: control characters, half a surrogate pair, U+FFFE and U+FFFF.</summary>
    public static string Clean(string text)
    {
        var kept = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                kept.Append(c).Append(text[++i]);
                continue;
            }
            if (char.IsSurrogate(c) || c is '￾' or '￿' || c < 0x20 && c is not ('\t' or '\n' or '\r'))
            {
                continue;
            }
            kept.Append(c);
        }
        return kept.ToString();
    }

    /// <summary>How people read an SSID: UTF-8 when it is, else each byte as Latin-1, as Windows' list shows odd ones.</summary>
    public static string NameOf(ReadOnlySpan<byte> ssid)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(ssid);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(ssid);
        }
    }

    private static bool IsHex(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsPrintableAscii(string text)
    {
        foreach (var c in text)
        {
            if (c is < ' ' or > '~')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Wi-Fi bars (1 to 4) for a WLAN signal quality: the split SystemStatus uses for the island.</summary>
    public static int Bars(uint quality) => SystemStatus.Bars(quality);

    /// <summary>The line under a network's name, as Windows' list words it.</summary>
    public static string StateText(bool connected, bool secured, bool internet = true) => connected
        ? secured
            ? internet ? S.DockQuickWifiConnectedSecured : S.DockQuickWifiNoInternetSecured
            : internet ? S.DockQuickConnected : S.DockQuickWifiNoInternetOpen
        : secured ? S.DockQuickWifiSecured : S.DockQuickWifiOpen;

    internal static string Invariant(uint value) => value.ToString(CultureInfo.InvariantCulture);
}
