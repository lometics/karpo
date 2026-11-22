// The quick-settings flyouts' pure parts: the slider's arithmetic, the Wi-Fi
// profile Windows is handed (escaping, keys, security), the Native Wifi structs'
// layouts, how lists are ordered and merged, the battery's words and numbers, and
// how a burst of system notifications becomes one post.

using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.QuickSettings;

/// <summary>
/// quick: the tick Karpo plays when its own volume controls settle, which Windows plays for its
/// own. Behind a seam, so nothing here makes a sound.
/// </summary>
public sealed class FeedbackTests : IDisposable
{
    private int ticks;

    // The waits a run of notches asked for, in ms: the test's clock, not a timer.
    private readonly List<int> armed = [];

    public FeedbackTests()
    {
        Feedback.Forget();
        Feedback.Play = _ => ticks++;
        Feedback.Arm = armed.Add;
    }

    public void Dispose()
    {
        Feedback.Play = null!;
        Feedback.Arm = null;
        Feedback.Forget();
    }

    /// <summary>
    /// The wheel spun over a volume control: thirty notches 40 ms apart are one run, and it plays
    /// nothing while it goes on and one tick once it has rested — not one a notch (backlog 16).
    /// </summary>
    [Fact]
    public void ASpunWheelTicksOnceAfterItRests()
    {
        var now = 1000L;
        for (var notch = 0; notch < 30; notch++)
        {
            Feedback.Notch(level: 50, muted: false, now: now);
            // The timer goes off now and then while the wheel turns: too soon, it waits on.
            Feedback.Settle(now + 10);
            now += 40;
        }
        Assert.Equal(0, ticks);
        Assert.True(Feedback.Waiting);
        var last = now - 40;
        // Not yet rested.
        Feedback.Settle(last + Feedback.QuietMs - 1);
        Assert.Equal(0, ticks);
        // Rested: the one tick, and nothing more however often the timer goes off.
        Feedback.Settle(last + Feedback.QuietMs);
        Assert.Equal(1, ticks);
        Feedback.Settle(last + (5 * Feedback.QuietMs));
        Assert.Equal(1, ticks);
        Assert.False(Feedback.Waiting);
        // Every notch asked for the whole quiet period again, which is what makes it a debounce.
        Assert.Contains(Feedback.QuietMs, armed);
    }

    /// <summary>A timer that went off early waits only for the rest of the quiet period.</summary>
    [Fact]
    public void AnEarlyTimerWaitsForTheRest()
    {
        Feedback.Notch(level: 50, muted: false, now: 1000);
        armed.Clear();
        Feedback.Settle(1000 + Feedback.QuietMs - 50);
        Assert.Equal([50], armed);
        Assert.Equal(0, ticks);
    }

    /// <summary>Notches far enough apart are runs of their own: a tick each.</summary>
    [Fact]
    public void NotchesThatRestBetweenThemTickEach()
    {
        var now = 1000L;
        for (var notch = 0; notch < 3; notch++)
        {
            Feedback.Notch(level: 50, muted: false, now: now);
            now += Feedback.QuietMs;
            Feedback.Settle(now);
            now += 500;
        }
        Assert.Equal(3, ticks);
    }

    /// <summary>A run that ends muted ends silent; one muted midway and unmuted by its last notch ticks.</summary>
    [Fact]
    public void ARunTicksAsItsLastNotchFoundTheOutput()
    {
        Feedback.Notch(level: 50, muted: false, now: 1000);
        Feedback.Notch(level: 50, muted: true, now: 1050);
        Feedback.Settle(1050 + Feedback.QuietMs);
        Assert.Equal(0, ticks);
        Feedback.Notch(level: 50, muted: true, now: 5000);
        Feedback.Notch(level: 50, muted: false, now: 5050);
        Feedback.Settle(5050 + Feedback.QuietMs);
        Assert.Equal(1, ticks);
    }

    /// <summary>A drag let go while notches were waiting is the one tick: the run's is not played on top of it.</summary>
    [Fact]
    public void ASettledLevelAnswersAWaitingRun()
    {
        Feedback.Notch(level: 50, muted: false, now: 1000);
        Feedback.Volume(level: 50, muted: false, now: 1100);
        Assert.Equal(1, ticks);
        Feedback.Settle(1100 + Feedback.QuietMs);
        Assert.Equal(1, ticks);
    }

    [Fact]
    public void ASettledLevelTicks()
    {
        Feedback.Volume(level: 50, muted: false, now: 1000);
        Assert.Equal(1, ticks);
    }

    [Fact]
    public void AMutedOutputIsSilent()
    {
        Feedback.Volume(level: 50, muted: true, now: 1000);
        Feedback.Volume(level: 50, muted: true, now: 5000);
        Assert.Equal(0, ticks);
    }

    [Fact]
    public void ASpinThatOutrunsTheSoundIsThinnedOut()
    {
        // Thirty notches in a few hundred milliseconds: the sound can't play thirty times over,
        // and Windows' own slider stutters rather than stacking them.
        var now = 1000L;
        for (var notch = 0; notch < 30; notch++)
        {
            Feedback.Volume(level: 50, muted: false, now: now);
            now += 10;
        }
        Assert.InRange(ticks, 4, 6);
        // A step on its own, a moment later, still ticks.
        Feedback.Volume(level: 50, muted: false, now: now + Feedback.GapMs);
        Assert.InRange(ticks, 5, 7);
    }

    /// <summary>
    /// "the sound that plays when you move the volume slider always has the same volume": the tick is
    /// played at the level it settled on, each time a different one, held to 0..100.
    /// </summary>
    [Fact]
    public void ASettledLevelIsPlayedAtThatLevel()
    {
        var played = new List<int>();
        Feedback.Play = played.Add;

        Feedback.Volume(level: 73, muted: false, now: 1000);
        Feedback.Volume(level: 12, muted: false, now: 2000);
        Feedback.Volume(level: 130, muted: false, now: 3000);

        Assert.Equal([73, 12, 100], played);
    }

    /// <summary>A run of notches ticks once, at the level its last notch left, not its first's.</summary>
    [Fact]
    public void ARunTicksAtTheLevelItsLastNotchLeft()
    {
        var played = new List<int>();
        Feedback.Play = played.Add;

        Feedback.Notch(level: 40, muted: false, now: 1000);
        Feedback.Notch(level: 42, muted: false, now: 1040);
        Feedback.Notch(level: 44, muted: false, now: 1080);
        Feedback.Settle(1080 + Feedback.QuietMs);

        Assert.Equal([44], played);
    }

    [Fact]
    public void StepsFarEnoughApartAllTick()
    {
        var now = 1000L;
        for (var step = 0; step < 4; step++)
        {
            Feedback.Volume(level: 50, muted: false, now: now);
            now += Feedback.GapMs + 1;
        }
        Assert.Equal(4, ticks);
    }
}

/// <summary>The busy ring's phase: the flyout's timer steps it, so nothing waits on DWM animating.</summary>
public sealed class SpinnerTests
{
    [Fact]
    public void ThePhaseWalksRoundAndComesBack()
    {
        Assert.Equal(0, Spinner.Next(352));
        Assert.Equal(8, Spinner.Next(0));
        Assert.Equal(4, Spinner.Next(356));
        // A step of its own, and one the other way.
        Assert.Equal(30, Spinner.Next(0, 30));
        Assert.Equal(350, Spinner.Next(0, -10));
        // A whole turn takes 1.5 seconds at the rate the flyout ticks.
        Assert.Equal(360 / Spinner.StepDegrees * Spinner.TickMs, 1485);
        var angle = 0f;
        for (var tick = 0; tick < 360 / (int)Spinner.StepDegrees; tick++)
        {
            angle = Spinner.Next(angle);
            Assert.InRange(angle, 0, 359.999f);
        }
        Assert.Equal(0, angle);
    }

    [Fact]
    public void NonsenseStartsOver()
    {
        Assert.Equal(0, Spinner.Next(float.NaN));
        Assert.Equal(0, Spinner.Next(float.PositiveInfinity));
        Assert.Equal(0, Spinner.Next(10, float.NaN));
    }
}

public sealed class SliderMathTests
{
    [Fact]
    public void AValueComesFromWhereThePointerIs()
    {
        Assert.Equal(0, SliderMath.ValueAt(-40, 10, 110, 0, 100));
        Assert.Equal(0, SliderMath.ValueAt(10, 10, 110, 0, 100));
        Assert.Equal(50, SliderMath.ValueAt(60, 10, 110, 0, 100));
        Assert.Equal(100, SliderMath.ValueAt(500, 10, 110, 0, 100));
        // Halves round up, as Karpo rounds everywhere.
        Assert.Equal(1, SliderMath.ValueAt(10.5f, 10, 110, 0, 100));
        // A track with no length, or a range with none, stays at its minimum.
        Assert.Equal(5, SliderMath.ValueAt(20, 10, 10, 5, 50));
        Assert.Equal(5, SliderMath.ValueAt(20, 10, 110, 5, 5));
    }

    [Fact]
    public void AValueSitsWhereItBelongs()
    {
        Assert.Equal(10, SliderMath.PositionOf(0, 10, 110, 0, 100));
        Assert.Equal(60, SliderMath.PositionOf(50, 10, 110, 0, 100));
        Assert.Equal(110, SliderMath.PositionOf(150, 10, 110, 0, 100));
        for (var value = 0; value <= 100; value++)
        {
            Assert.Equal(value, SliderMath.ValueAt(SliderMath.PositionOf(value, 12, 337, 0, 100), 12, 337, 0, 100));
        }
    }

    [Fact]
    public void KeysStepIt()
    {
        Assert.Equal(41, SliderMath.Key(40, VK.VK_RIGHT, 0, 100, 1, 10));
        Assert.Equal(41, SliderMath.Key(40, VK.VK_UP, 0, 100, 1, 10));
        Assert.Equal(39, SliderMath.Key(40, VK.VK_LEFT, 0, 100, 1, 10));
        Assert.Equal(39, SliderMath.Key(40, VK.VK_DOWN, 0, 100, 1, 10));
        Assert.Equal(50, SliderMath.Key(40, VK.VK_PRIOR, 0, 100, 1, 10));
        Assert.Equal(30, SliderMath.Key(40, VK.VK_NEXT, 0, 100, 1, 10));
        Assert.Equal(0, SliderMath.Key(40, VK.VK_HOME, 0, 100, 1, 10));
        Assert.Equal(100, SliderMath.Key(40, VK.VK_END, 0, 100, 1, 10));
        Assert.Equal(100, SliderMath.Key(95, VK.VK_PRIOR, 0, 100, 1, 10));
        Assert.Equal(0, SliderMath.Key(3, VK.VK_NEXT, 0, 100, 1, 10));
        Assert.Null(SliderMath.Key(40, VK.VK_TAB, 0, 100, 1, 10));
    }

    [Fact]
    public void TheWheelStepsByNotchesAndKeepsWhatIsLeft()
    {
        var rest = 0;
        Assert.Equal(52, SliderMath.Wheel(50, 120, ref rest, 2, 0, 100));
        Assert.Equal(0, rest);
        // A precise wheel's small turns add up to a notch.
        Assert.Equal(52, SliderMath.Wheel(52, 60, ref rest, 2, 0, 100));
        Assert.Equal(60, rest);
        Assert.Equal(54, SliderMath.Wheel(52, 60, ref rest, 2, 0, 100));
        Assert.Equal(0, rest);
        Assert.Equal(50, SliderMath.Wheel(54, -240, ref rest, 2, 0, 100));
        Assert.Equal(100, SliderMath.Wheel(99, 360, ref rest, 2, 0, 100));
        Assert.Equal(0, SliderMath.Wheel(1, -120, ref rest, 2, 0, 100));
    }
}

public sealed class WifiProfileTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void SecurityComesFromTheDefaultAlgorithms()
    {
        Assert.Equal(WifiSecurity.Open, WifiProfiles.SecurityOf(1, 0));
        Assert.Equal(WifiSecurity.Wep, WifiProfiles.SecurityOf(1, 1));
        Assert.Equal(WifiSecurity.Wep, WifiProfiles.SecurityOf(2, 5));
        Assert.Equal(WifiSecurity.WpaPersonal, WifiProfiles.SecurityOf(4, 2));
        Assert.Equal(WifiSecurity.Wpa2Personal, WifiProfiles.SecurityOf(7, 4));
        Assert.Equal(WifiSecurity.Wpa3Personal, WifiProfiles.SecurityOf(9, 4));
        Assert.Equal(WifiSecurity.Owe, WifiProfiles.SecurityOf(10, 4));
        Assert.Equal(WifiSecurity.Enterprise, WifiProfiles.SecurityOf(6, 4));
        Assert.Equal(WifiSecurity.Enterprise, WifiProfiles.SecurityOf(11, 8));
        // An enterprise network joins with a user name and a password — WPA, WPA2, WPA3-Enterprise —
        // and one that signs in with a certificate alone (WPA3-Enterprise 192-bit) does not.
        Assert.True(WifiProfiles.CanJoin(WifiSecurity.Enterprise, 6));
        Assert.False(WifiProfiles.CanJoin(WifiSecurity.Enterprise, 8));
        Assert.True(WifiProfiles.CanJoin(WifiSecurity.Wpa2Personal, 0));
        Assert.True(WifiProfiles.NeedsKey(WifiSecurity.Wpa3Personal));
        Assert.False(WifiProfiles.NeedsKey(WifiSecurity.Owe));
    }

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("correct horse battery staple", true)]
    [InlineData("äpfel und birnen", false)]
    [InlineData("0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789abcdeg", false)]
    public void WpaKeysAreChecked(string key, bool fine)
    {
        Assert.Equal(fine, WifiProfiles.KeyProblem(WifiSecurity.Wpa2Personal, key) is null);
        Assert.Equal(fine, WifiProfiles.KeyProblem(WifiSecurity.Wpa3Personal, key) is null);
    }

    [Theory]
    [InlineData("abcde", true)]
    [InlineData("abcdefghijklm", true)]
    [InlineData("0a1b2c3d4e", true)]
    [InlineData("0a1b2c3d4e5f6a7b8c9d0e1f2a", true)]
    [InlineData("abcdef", false)]
    [InlineData("0a1b2c3d4g", false)]
    public void WepKeysAreChecked(string key, bool fine) =>
        Assert.Equal(fine, WifiProfiles.KeyProblem(WifiSecurity.Wep, key) is null);

    [Fact]
    public void TheProfileIsValidXmlWithTheNameAndKeyEscaped()
    {
        var ssid = "Tom & Jerry's <Café> \"5G\"";
        var key = "p&ss<wo>rd\"'x";
        var xml = WifiProfiles.Xml(Bytes(ssid), ssid, WifiSecurity.Wpa2Personal, 4, key, automatic: true);
        var document = XDocument.Parse(xml);
        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";
        var profile = document.Root!;
        Assert.Equal(ssid, profile.Element(ns + "name")!.Value);
        var ssidConfig = profile.Element(ns + "SSIDConfig")!.Element(ns + "SSID")!;
        Assert.Equal(Convert.ToHexString(Bytes(ssid)), ssidConfig.Element(ns + "hex")!.Value);
        Assert.Equal(ssid, ssidConfig.Element(ns + "name")!.Value);
        Assert.Equal("auto", profile.Element(ns + "connectionMode")!.Value);
        var security = profile.Element(ns + "MSM")!.Element(ns + "security")!;
        var auth = security.Element(ns + "authEncryption")!;
        Assert.Equal("WPA2PSK", auth.Element(ns + "authentication")!.Value);
        Assert.Equal("AES", auth.Element(ns + "encryption")!.Value);
        var shared = security.Element(ns + "sharedKey")!;
        Assert.Equal("passPhrase", shared.Element(ns + "keyType")!.Value);
        Assert.Equal("false", shared.Element(ns + "protected")!.Value);
        Assert.Equal(key, shared.Element(ns + "keyMaterial")!.Value);
    }

    [Fact]
    public void EachSecurityGetsItsOwnElements()
    {
        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";
        (string Auth, string Encryption, bool Key) Read(WifiSecurity security, int cipher, string? key)
        {
            var root = XDocument.Parse(WifiProfiles.Xml(Bytes("net"), "net", security, cipher, key, automatic: false)).Root!;
            Assert.Equal("manual", root.Element(ns + "connectionMode")!.Value);
            var security2 = root.Element(ns + "MSM")!.Element(ns + "security")!;
            var auth = security2.Element(ns + "authEncryption")!;
            return (auth.Element(ns + "authentication")!.Value, auth.Element(ns + "encryption")!.Value, security2.Element(ns + "sharedKey") is not null);
        }
        Assert.Equal(("open", "none", false), Read(WifiSecurity.Open, 0, null));
        Assert.Equal(("OWE", "AES", false), Read(WifiSecurity.Owe, 4, null));
        Assert.Equal(("WPA3SAE", "AES", true), Read(WifiSecurity.Wpa3Personal, 4, "12345678"));
        Assert.Equal(("WPA2PSK", "TKIP", true), Read(WifiSecurity.Wpa2Personal, 2, "12345678"));
        Assert.Equal(("WPAPSK", "AES", true), Read(WifiSecurity.WpaPersonal, 4, "12345678"));
        Assert.Equal(("open", "WEP", true), Read(WifiSecurity.Wep, 1, "abcde"));
        // A key for an open network isn't written.
        Assert.Equal(("open", "none", false), Read(WifiSecurity.Open, 0, "ignored!"));
    }

    [Fact]
    public void A64DigitKeyIsTheKeyItselfAndWepIsAlwaysRaw()
    {
        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";
        string KeyType(WifiSecurity security, string key) => XDocument.Parse(WifiProfiles.Xml(Bytes("n"), "n", security, 4, key, true)).Root!
            .Element(ns + "MSM")!.Element(ns + "security")!.Element(ns + "sharedKey")!.Element(ns + "keyType")!.Value;
        Assert.Equal("networkKey", KeyType(WifiSecurity.Wpa2Personal, new string('a', 64)));
        Assert.Equal("passPhrase", KeyType(WifiSecurity.Wpa2Personal, new string('a', 63)));
        Assert.Equal("networkKey", KeyType(WifiSecurity.Wep, "abcde"));
    }

    [Fact]
    public void WhatXmlCantCarryIsLeftOut()
    {
        var name = "bad\u0001name\uD800\uFFFF";
        Assert.Equal("badname", WifiProfiles.Clean(name));
        Assert.Equal("a&amp;b&lt;c&gt;d&quot;e&apos;f", WifiProfiles.Escape("a&b<c>d\"e'f"));
        // A pair stays whole.
        Assert.Equal("x\U0001F600", WifiProfiles.Clean("x\U0001F600"));
        var xml = WifiProfiles.Xml(Bytes(name), name, WifiSecurity.Open, 0, null, true);
        Assert.NotNull(XDocument.Parse(xml));
    }

    [Fact]
    public void AnSsidWithoutAReadableNameIsNamedByItsHex()
    {
        var bytes = new byte[] { 0x01, 0x02, 0xFF };
        Assert.Equal("0102FF", WifiProfiles.ProfileName(bytes, "\u0001\u0002"));
        Assert.Equal("0102FF", WifiProfiles.ProfileName(bytes, "   "));
        Assert.Equal("Home", WifiProfiles.ProfileName(Bytes("Home"), "Home"));
    }

    [Fact]
    public void SsidsAreReadAsUtf8ElseLatin1()
    {
        Assert.Equal("Café", WifiProfiles.NameOf(Bytes("Café")));
        Assert.Equal("Caf\u00E9", WifiProfiles.NameOf([0x43, 0x61, 0x66, 0xE9]));
    }

    [Fact]
    public void TheStateLineReadsAsWindowsSaysIt()
    {
        Assert.Equal("Connected, secured", WifiProfiles.StateText(connected: true, secured: true));
        Assert.Equal("Connected", WifiProfiles.StateText(connected: true, secured: false));
        Assert.Equal("Secured", WifiProfiles.StateText(connected: false, secured: true));
        Assert.Equal("Open", WifiProfiles.StateText(connected: false, secured: false));
        Assert.Equal("No internet, secured", WifiProfiles.StateText(connected: true, secured: true, internet: false));
    }

    [Fact]
    public void TheConnectedNetworkComesFirstThenTheStrongest()
    {
        WifiNetwork Net(string name, int bars, bool connected = false) => new(name, Bytes(name), bars, WifiSecurity.Wpa2Personal, 4, connected, null);
        var sorted = WindowsWifi.Sort([Net("b", 2), Net("a", 2), Net("weak", 1, connected: true), Net("strong", 4)]);
        Assert.Equal(["weak", "strong", "a", "b"], sorted.Select(network => network.Ssid));
    }
}

public sealed unsafe class WlanLayoutTests
{
    [Fact]
    public void TheStructsHaveWlanapisSizes()
    {
        Assert.Equal(532, sizeof(WindowsWifi.InterfaceInfo));
        Assert.Equal(36, sizeof(WindowsWifi.Dot11Ssid));
        Assert.Equal(628, sizeof(WindowsWifi.AvailableNetwork));
        Assert.Equal(12, sizeof(WindowsWifi.PhyRadioState));
        Assert.Equal(40, sizeof(WindowsWifi.ConnectionParameters));
    }

    [Fact]
    public void TheFieldsSitWhereWlanapiPutsThem()
    {
        var network = default(WindowsWifi.AvailableNetwork);
        var start = (byte*)&network;
        Assert.Equal(512, (int)((byte*)&network.Ssid - start));
        Assert.Equal(604, (int)((byte*)&network.SignalQuality - start));
        Assert.Equal(612, (int)((byte*)&network.AuthAlgorithm - start));
        Assert.Equal(616, (int)((byte*)&network.CipherAlgorithm - start));
        Assert.Equal(620, (int)((byte*)&network.Flags - start));
        var parameters = default(WindowsWifi.ConnectionParameters);
        var begin = (byte*)&parameters;
        Assert.Equal(8, (int)((byte*)&parameters.Profile - begin));
        Assert.Equal(16, (int)((byte*)&parameters.Ssid - begin));
        Assert.Equal(32, (int)((byte*)&parameters.BssType - begin));
        Assert.Equal(16, Unsafe.SizeOf<Guid>());
    }
}

public sealed class SoundTests
{
    [Fact]
    public void AnAppIsKeyedByItsPackageElseItsExecutable()
    {
        Assert.Equal("aumid:spotify.app!spotify", WindowsSound.KeyOf("Spotify.App!Spotify", @"C:\x\Spotify.exe", 12));
        Assert.Equal(@"exe:c:\program files\firefox\firefox.exe", WindowsSound.KeyOf(null, @"C:\Program Files\Firefox\firefox.exe", 12));
        Assert.Equal("pid:12", WindowsSound.KeyOf(null, null, 12));
        Assert.Equal("pid:12", WindowsSound.KeyOf("", "", 12));
    }

    [Fact]
    public void SystemSoundsComeFirstThenTheAppsByName()
    {
        var apps = WindowsSound.Order(
        [
            new("b", "spotify", 1, false, false, null, null),
            new("s", "System sounds", 1, false, true, null, null),
            new("a", "Firefox", 1, false, false, null, null),
        ]);
        Assert.Equal(["System sounds", "Firefox", "spotify"], apps.Select(app => app.Name));
    }

    [Fact]
    public void AWinRtDevicePathGivesItsEndpointId()
    {
        Assert.Equal("{0.0.0.00000000}.{abc}", WindowsSound.EndpointId(@"\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{abc}#{e6327cad-dcec-4949-ae8a-991e976a79d2}"));
        Assert.Equal("{0.0.0.00000000}.{abc}", WindowsSound.EndpointId("{0.0.0.00000000}.{abc}"));
    }

    [Fact]
    public void TheSpeakerGlyphFollowsTheLevel()
    {
        Assert.Equal(char.ConvertFromUtf32(0xE74F), SoundRows.LevelGlyph(80, muted: true));
        Assert.Equal(char.ConvertFromUtf32(0xE992), SoundRows.LevelGlyph(0, false));
        Assert.Equal(char.ConvertFromUtf32(0xE993), SoundRows.LevelGlyph(33, false));
        Assert.Equal(char.ConvertFromUtf32(0xE994), SoundRows.LevelGlyph(34, false));
        Assert.Equal(char.ConvertFromUtf32(0xE995), SoundRows.LevelGlyph(67, false));
    }

    [Fact]
    public void AnOutputsGlyphFollowsItsFormFactor()
    {
        Assert.Equal(char.ConvertFromUtf32(0xE7F6), WindowsSound.GlyphFor(3));
        Assert.Equal(char.ConvertFromUtf32(0xE95B), WindowsSound.GlyphFor(5));
        Assert.Equal(char.ConvertFromUtf32(0xE7F4), WindowsSound.GlyphFor(9));
        Assert.Equal(char.ConvertFromUtf32(0xE7F5), WindowsSound.GlyphFor(1));
        Assert.Equal(char.ConvertFromUtf32(0xE7F5), WindowsSound.GlyphFor(-1));
    }
}

public sealed class BatteryTests
{
    private static BatteryState State(bool plugged, bool charging, int percent = 50, TimeSpan? remaining = null, TimeSpan? untilFull = null) =>
        new(true, percent, plugged, charging, remaining, untilFull, false, PowerMode.Balanced);

    [Fact]
    public void TheStatusReadsAsWindowsSaysIt()
    {
        Assert.Equal("On battery", BatteryText.StatusText(State(plugged: false, charging: false)));
        Assert.Equal("Charging", BatteryText.StatusText(State(plugged: true, charging: true)));
        Assert.Equal("Plugged in, not charging", BatteryText.StatusText(State(plugged: true, charging: false)));
        Assert.Equal("Fully charged", BatteryText.StatusText(State(plugged: true, charging: false, percent: 100)));
    }

    [Fact]
    public void TimeIsInHoursAndMinutes()
    {
        Assert.Equal("3 h 17 min", BatteryText.Span(TimeSpan.FromMinutes(197)));
        Assert.Equal("45 min", BatteryText.Span(TimeSpan.FromMinutes(45)));
        Assert.Equal("2 h", BatteryText.Span(TimeSpan.FromMinutes(120)));
        Assert.Equal("1 min", BatteryText.Span(TimeSpan.FromSeconds(10)));
        Assert.Equal("1 h", BatteryText.Span(TimeSpan.FromSeconds(3599)));
        Assert.Equal("About 3 h 17 min remaining", BatteryText.TimeText(State(false, false, remaining: TimeSpan.FromMinutes(197))));
        Assert.Equal("About 45 min until fully charged", BatteryText.TimeText(State(true, true, untilFull: TimeSpan.FromMinutes(45))));
        Assert.Null(BatteryText.TimeText(State(true, false, remaining: TimeSpan.FromMinutes(5))));
        Assert.Null(BatteryText.TimeText(State(false, false)));
    }

    [Fact]
    public void TheTimeToFullComesFromTheChargeRate()
    {
        Assert.Equal(TimeSpan.FromHours(0.5), WindowsBattery.EstimateUntilFull(40_000, 50_000, 20_000));
        Assert.Null(WindowsBattery.EstimateUntilFull(50_000, 50_000, 20_000));
        Assert.Null(WindowsBattery.EstimateUntilFull(10_000, 50_000, 0));
        Assert.Null(WindowsBattery.EstimateUntilFull(10_000, 50_000, -5_000));
        // A trickle that would take days says nothing.
        Assert.Null(WindowsBattery.EstimateUntilFull(0, 50_000, 10));
    }

    [Fact]
    public void AnOverlayIsAPowerMode()
    {
        Assert.Equal(PowerMode.Efficiency, WindowsBattery.ModeOf(WindowsBattery.BestEfficiency));
        Assert.Equal(PowerMode.Performance, WindowsBattery.ModeOf(WindowsBattery.BestPerformance));
        Assert.Equal(PowerMode.Balanced, WindowsBattery.ModeOf(Guid.Empty));
        Assert.Equal(PowerMode.Balanced, WindowsBattery.ModeOf(new Guid("3AF9B8D9-7C97-431D-AD78-34A8BFEA439F")));
    }
}

public sealed class BluetoothTests
{
    [Fact]
    public void PairedDevicesComeFirstConnectedOnesFirstOfAll()
    {
        var sorted = WindowsBluetooth.Sort(
        [
            new("1", "zebra", "", Paired: false, Connected: false),
            new("2", "Mouse", "", Paired: true, Connected: false),
            new("3", "Headphones", "", Paired: true, Connected: true),
            new("4", "apple", "", Paired: false, Connected: false),
            new("5", "Keyboard", "", Paired: true, Connected: false),
        ]);
        Assert.Equal(["Headphones", "Keyboard", "Mouse", "apple", "zebra"], sorted.Select(device => device.Name));
    }

    private static readonly WindowsBluetooth.Search ConnectedOnes = WindowsBluetooth.Searches.Single(search => search.Connected == true);
    private static readonly WindowsBluetooth.Search OtherOnes = WindowsBluetooth.Searches.Single(search => search.Connected == false);

    /// <summary>What a watcher hands over for a device, as WindowsBluetooth.Row builds it.</summary>
    private static void Seen(BluetoothList list, WindowsBluetooth.Search search, string id) =>
        list.Seen(search, id, new BluetoothDevice(id, id, "G", Paired: true, search.Connected ?? false));

    /// <summary>The list as the page shows it at <paramref name="now"/>: ids in order, a "+" on the connected ones.</summary>
    private static List<string> Shown(BluetoothList list, long now) =>
        [.. WindowsBluetooth.Sort(list.Read(now)).Select(device => device.Connected ? device.Id + "+" : device.Id)];

    /// <summary>
    /// The user: "when connecting a device it shortly disappears from the bluetooth device list."
    /// The paired devices are two watchers, connected and not, so a device connecting leaves one
    /// and joins the other, each on its own pool thread, in either order. At no step may it leave
    /// the list or its place: it is "Connecting…" until it arrives, and connected after. Going the
    /// other way too, which is a headset switched off.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ADeviceMovingBetweenWatchersNeverLeavesTheList(bool connecting, bool leavesFirst)
    {
        var list = new BluetoothList();
        var (from, to) = connecting ? (OtherOnes, ConnectedOnes) : (ConnectedOnes, OtherOnes);
        Seen(list, ConnectedOnes, "Alpha");
        Seen(list, from, "Headset");
        Seen(list, OtherOnes, "Keyboard");
        var before = Shown(list, 0);
        var after = before.Select(id => id.StartsWith("Headset") ? connecting ? "Headset+" : "Headset" : id).ToList();

        var steps = new List<List<string>>();
        if (leavesFirst)
        {
            list.Gone(from, "Headset", now: 10);
            steps.Add(Shown(list, 20));
            Seen(list, to, "Headset");
        }
        else
        {
            Seen(list, to, "Headset");
            steps.Add(Shown(list, 20));
            list.Gone(from, "Headset", now: 30);
        }
        steps.Add(Shown(list, 40));
        // And the watcher it left saying one more thing on its way out changes nothing.
        steps.Add(Shown(list, 10_000));

        // Between watchers it is where it was, as it was, or already where it is going.
        Assert.True(steps[0].SequenceEqual(before) || steps[0].SequenceEqual(after), string.Join(" ", steps[0]));
        Assert.Equal(after, steps[1]);
        Assert.Equal(after, steps[2]);
    }

    /// <summary>A watcher's late word from a device it is losing does not undo the other's news.</summary>
    [Fact]
    public void ALateWordFromTheWatcherLeftDoesNotDisconnect()
    {
        var list = new BluetoothList();
        Seen(list, OtherOnes, "Headset");
        Seen(list, ConnectedOnes, "Headset");
        Seen(list, OtherOnes, "Headset");
        list.Gone(OtherOnes, "Headset", now: 0);
        Assert.Equal(["Headset+"], Shown(list, 10));
        Assert.Equal(["Headset+"], Shown(list, 10_000));
    }

    /// <summary>
    /// A device no watcher takes back is gone once it has been waited for, and one unpaired here is
    /// gone at once: there is nothing to wait for.
    /// </summary>
    [Fact]
    public void ADeviceNobodyTakesBackIsGoneAfterAWhile()
    {
        var list = new BluetoothList();
        Seen(list, OtherOnes, "Headset");
        Seen(list, OtherOnes, "Keyboard");
        list.Gone(OtherOnes, "Headset", now: 1_000);
        Assert.Equal(["Headset", "Keyboard"], Shown(list, 1_000 + BluetoothList.MovingMs - 1));
        Assert.Equal(["Keyboard"], Shown(list, 1_000 + BluetoothList.MovingMs));

        list.Forget("Keyboard");
        Assert.Empty(Shown(list, 1_000 + BluetoothList.MovingMs));
    }

    /// <summary>What a reading found survives the move: the kind, the level, and that it is audio.</summary>
    [Fact]
    public void WhatWasReadSurvivesTheMove()
    {
        var list = new BluetoothList();
        Seen(list, OtherOnes, "Headset");
        Assert.True(list.Learned("Headset", 72, "H", audio: true));
        list.Gone(OtherOnes, "Headset", now: 0);
        Seen(list, ConnectedOnes, "Headset");
        var device = Assert.Single(list.Read(10));
        Assert.Equal((72, "H", true, true), (device.Battery, device.Glyph, device.Audio, device.Connected));
        Assert.True(list.Walked("Headset"));
    }

    [Fact]
    public void AnOutcomeReadsAsWords()
    {
        Assert.Equal("authentication timeout", WindowsBluetooth.Words("AuthenticationTimeout"));
        Assert.Equal("failed", WindowsBluetooth.Words("Failed"));
    }

    private static BluetoothDevice Device(int? battery, bool connected = true) =>
        new("id", "WH-1000XM4", "G", Paired: true, connected, battery);

    /// <summary>A device that gives a level shows it; one that gives none shows nothing at all.</summary>
    [Fact]
    public void ALevelIsShownAndNothingIsNot()
    {
        Assert.Equal("72%", BluetoothPage.Level(Device(72)));
        Assert.Null(BluetoothPage.Level(Device(null)));
    }

    /// <summary>
    /// And a device that really says zero says zero: "nothing" and "empty" are different answers,
    /// which is the whole reason the level is a nullable number rather than a count.
    /// </summary>
    [Fact]
    public void ZeroIsNotNothing()
    {
        Assert.Equal("0%", BluetoothPage.Level(Device(0)));
        Assert.NotEqual(BluetoothPage.Level(Device(0)), BluetoothPage.Level(Device(null)));
    }

    /// <summary>
    /// Windows hands the level over as a byte on the device node that carries it, and hands back
    /// the key with a null value on the many nodes that do not. Anything that is not a percentage
    /// is not a level: no guessing, no clamping a wild number into range.
    /// </summary>
    [Theory]
    [InlineData((byte)20, 20)]
    [InlineData((byte)0, 0)]
    [InlineData((byte)100, 100)]
    [InlineData((byte)255, null)]
    [InlineData(47, 47)]
    [InlineData(-1, null)]
    [InlineData(101, null)]
    [InlineData("80", null)]
    [InlineData(null, null)]
    public void OnlyAPercentageIsALevel(object? value, int? expected) =>
        Assert.Equal(expected, BluetoothNodes.Percent(value));

    /// <summary>
    /// A level never changes where a device sits: the list is ordered by what it is (paired,
    /// connected, name), so a battery arriving a second after the list did cannot reorder it under
    /// the pointer.
    /// </summary>
    [Fact]
    public void ALevelDoesNotMoveADevice()
    {
        List<BluetoothDevice> devices =
        [
            new("1", "Keychron", "G", Paired: true, Connected: false),
            new("2", "WH-1000XM4", "G", Paired: true, Connected: true),
            new("3", "JBL", "G", Paired: false, Connected: false),
        ];

        var before = WindowsBluetooth.Sort([.. devices]).Select(device => device.Id).ToList();
        devices[1] = devices[1] with { Battery = 72 };
        var after = WindowsBluetooth.Sort([.. devices]).Select(device => device.Id).ToList();

        Assert.Equal(before, after);
        Assert.Equal(["2", "1", "3"], after);
    }

    /// <summary>
    /// A card offers to connect or disconnect a paired audio device only, as Windows' own page does:
    /// disconnect while it is connected, connect while it is not; nothing for a mouse, a phone or a
    /// device not yet paired, and nothing for one whose nodes have not said what it is.
    /// </summary>
    [Fact]
    public void OnlyAPairedAudioDeviceIsConnectedFromItsCard()
    {
        var headset = new BluetoothDevice("1", "WH-1000XM4", "G", Paired: true, Connected: true, Audio: true);
        Assert.True(BluetoothPage.Offers(headset));
        Assert.False(BluetoothPage.Offers(headset with { Connected = false }));
        Assert.Null(BluetoothPage.Offers(headset with { Audio = false }));
        Assert.Null(BluetoothPage.Offers(headset with { Paired = false, Connected = false }));

        Assert.Equal("Connecting…", BluetoothPage.Working(true));
        Assert.Equal("Disconnecting…", BluetoothPage.Working(false));
        Assert.Equal("Removing…", BluetoothPage.Working(null));
    }

    /// <summary>What makes a device an audio one, and which endpoints are Bluetooth's: its nodes' classes, its enumerator.</summary>
    [Fact]
    public void AnAudioDeviceIsKnownByItsNodes()
    {
        Assert.True(WindowsBluetooth.IsAudio([WindowsBluetooth.Media]));
        Assert.True(WindowsBluetooth.IsAudio([WindowsBluetooth.Anything, WindowsBluetooth.AudioEndpoint]));
        Assert.False(WindowsBluetooth.IsAudio([WindowsBluetooth.Mouse]));
        Assert.False(WindowsBluetooth.IsAudio([]));

        Assert.True(BluetoothAudio.IsBluetooth("BTHENUM"));
        Assert.True(BluetoothAudio.IsBluetooth("bthhfenum"));
        Assert.False(BluetoothAudio.IsBluetooth("HDAUDIO"));
        Assert.False(BluetoothAudio.IsBluetooth(null));
        // The request's set and numbers are ksmedia.h's.
        Assert.Equal(new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D"), BluetoothAudio.PropertySet);
        Assert.Equal((0u, 1u), (BluetoothAudio.Reconnect, BluetoothAudio.Disconnect));
    }

    /// <summary>A device says nothing about its battery until something says otherwise.</summary>
    [Fact]
    public void ADeviceStartsWithNoLevel() =>
        Assert.Null(new BluetoothDevice("1", "Thing", "G", Paired: true, Connected: true).Battery);

    /// <summary>
    /// A row's picture comes from the setup classes of the device's own nodes. The first class
    /// that names something wins, the ones every Bluetooth device carries name nothing, and a
    /// device whose nodes name nothing keeps the plain rune: a wrong picture is worse than none.
    /// </summary>
    [Fact]
    public void AGlyphFollowsTheDevicesKind()
    {
        var plain = char.ConvertFromUtf32(0xE702);
        Assert.Equal(char.ConvertFromUtf32(0xE7F6), WindowsBluetooth.GlyphFor([WindowsBluetooth.Media]));
        Assert.Equal(char.ConvertFromUtf32(0xE962), WindowsBluetooth.GlyphFor([WindowsBluetooth.Mouse]));
        Assert.Equal(char.ConvertFromUtf32(0xE765), WindowsBluetooth.GlyphFor([WindowsBluetooth.Keyboard]));
        // A headset's nodes are Media, System and Bluetooth, in whatever order they turn up: the
        // one that names something wins wherever it sits.
        Assert.Equal(
            char.ConvertFromUtf32(0xE7F6),
            WindowsBluetooth.GlyphFor([WindowsBluetooth.Anything, Guid.Empty, WindowsBluetooth.Media]));
        // Nothing said, and nothing that says anything: the plain rune, not a guess.
        Assert.Equal(plain, WindowsBluetooth.GlyphFor([]));
        Assert.Equal(plain, WindowsBluetooth.GlyphFor([WindowsBluetooth.Anything]));
    }

    /// <summary>
    /// The made-up devices draw themselves the way the real ones do, or the offscreen pictures
    /// drift away from the panel a person actually sees — which is exactly what happened while
    /// the real rows had no kind at all and the sheets went on showing headsets and mice.
    /// </summary>
    [Fact]
    public void TheMadeUpDevicesDrawWhatTheRealOnesWould()
    {
        var plain = WindowsBluetooth.GlyphFor([]);
        var drawn = new SampleBluetooth().Read().Devices;
        Assert.NotEmpty(drawn);
        foreach (var device in drawn)
        {
            Assert.Contains(
                device.Glyph,
                (string[])
                [
                    WindowsBluetooth.GlyphFor([WindowsBluetooth.Media]),
                    WindowsBluetooth.GlyphFor([WindowsBluetooth.Mouse]),
                    WindowsBluetooth.GlyphFor([WindowsBluetooth.Keyboard]),
                    WindowsBluetooth.GlyphFor([WindowsBluetooth.Printer]),
                    plain,
                ]);
        }
        // The panel lists paired devices only, and so do its pictures.
        Assert.All(drawn, device => Assert.True(device.Paired, $"{device.Name} is not paired"));
    }

    /// <summary>
    /// The searches have to say between them what a row needs, because nothing else can: the
    /// property list that used to carry it cannot cross to WinRT in the published bar (see the
    /// header of Bluetooth.cs). A paired device must be found by a search that knows whether it is
    /// connected, or the panel is back to showing a headset as idle while it plays.
    /// </summary>
    [Fact]
    public void EverySearchSaysWhatItFinds()
    {
        var searches = WindowsBluetooth.Searches;
        Assert.All(searches, search => Assert.Contains("System.Devices.Aep.ProtocolId", search.Query));
        Assert.All(searches.Where(search => search.Paired), search => Assert.NotNull(search.Connected));
        Assert.Equal([false, true], searches.Where(search => search.Paired).Select(search => search.Connected!.Value).Order());
    }

    /// <summary>
    /// The flyout lists paired devices only: no search looks for devices that are not, so the radio
    /// is not kept scanning while the flyout is open. A new device is paired in Windows' own "Add a
    /// device" dialog.
    /// </summary>
    [Fact]
    public void NothingLooksForNewDevices()
    {
        Assert.All(WindowsBluetooth.Searches, search =>
        {
            Assert.True(search.Paired);
            Assert.Contains("System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True", search.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#False", search.Query, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// "Add device" opens Windows' Bluetooth settings, never ms-settings-connectabledevices:, which
    /// Windows 11 answers with the Cast panel (the person: "when i click add bluetooth device the
    /// cast opens"): the action the Bluetooth preset's footer has for it, and no other action opens
    /// that address.
    /// </summary>
    [Fact]
    public void AddDeviceOpensBluetoothSettingsNotCast()
    {
        var add = SettingsActions.Find(SettingsActions.AddDeviceId)!;
        Assert.Equal("ms-settings:bluetooth", add.Uri);
        Assert.Equal("Add device", add.Name);
        Assert.Equal(SettingsActions.AddDeviceId, Presets.Find(Presets.BluetoothId)!.Items.Single(item => item.Premade is not null).Premade!.Actions!.Value.Right);
        Assert.All(SettingsActions.All, action => Assert.DoesNotContain("connectabledevices", action.Uri, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A level's glyph is Windows' own battery, filled in tenths to the nearest as its battery icon
    /// is (SystemStatus.Describe): empty at 0 to 4 %, full from 95 %; a device that says nothing has
    /// no glyph, as it has no percentage.
    /// </summary>
    [Theory]
    [InlineData(0, 0xE850)]
    [InlineData(4, 0xE850)]
    [InlineData(5, 0xE851)]
    [InlineData(15, 0xE852)]
    [InlineData(45, 0xE855)]
    [InlineData(72, 0xE857)]
    [InlineData(94, 0xE859)]
    [InlineData(95, 0xE83F)]
    [InlineData(100, 0xE83F)]
    public void ABatteryGlyphIsFilledToTheLevel(int percent, int glyph)
    {
        var device = new BluetoothDevice("1", "WH-1000XM5", "G", Paired: true, Connected: true, Battery: percent);
        Assert.Equal(char.ConvertFromUtf32(glyph), BluetoothPage.BatteryGlyph(device));
        Assert.Equal($"{percent}%", BluetoothPage.Level(device));
        Assert.Null(BluetoothPage.BatteryGlyph(device with { Battery = null }));
        Assert.Null(BluetoothPage.Level(device with { Battery = null }));
    }

    /// <summary>
    /// The one shape no test can watch fail: a managed collection handed to a WinRT call works
    /// under the JIT every test runs in, and throws in the published native-AOT bar, where no CCW
    /// is generated for it. It cost the user a flyout that said "no devices" with a headset
    /// connected. So the sources are read instead, and the rule (docs\widgets.md section 10) is
    /// checked where it can be: these calls take a property list, and it must be null.
    /// </summary>
    [Fact]
    public void NoPropertyListIsHandedToWindows()
    {
        var folder = Dock();
        Assert.True(Directory.Exists(folder), $"Karpo.Dock's sources are not at {folder}.");
        var offenders = new List<string>();
        var seen = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (var (line, arguments) in Calls(File.ReadAllLines(file)))
            {
                seen++;
                if (arguments.Count >= 2 && arguments[1] != "null")
                {
                    offenders.Add($"{Path.GetFileName(file)}:{line} asks for {arguments[1]}");
                }
            }
        }
        // A scan that finds nothing to check has stopped checking: these calls exist.
        Assert.True(seen > 0, "No device-enumeration call was found to check.");
        Assert.Empty(offenders);
    }

    /// <summary>Karpo.Dock's own folder, in the repository.</summary>
    private static string Dock() => Path.Combine(RepoSource.Root(), "Karpo.Dock");

    /// <summary>
    /// The arguments of every device-enumeration call in <paramref name="lines"/>, by line number.
    /// Whole-line comments are skipped: this file's own header quotes the call it is warning about.
    /// </summary>
    private static IEnumerable<(int Line, List<string> Arguments)> Calls(string[] lines)
    {
        string[] takesAList = ["CreateWatcher(", "FindAllAsync(", "CreateFromIdAsync("];
        for (var index = 0; index < lines.Length; index++)
        {
            var text = lines[index];
            if (text.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (var call in takesAList)
            {
                var at = text.IndexOf(call, StringComparison.Ordinal);
                if (at >= 0 && Arguments(text, at + call.Length) is { } arguments)
                {
                    yield return (index + 1, arguments);
                }
            }
        }
    }

    /// <summary>The top-level arguments from <paramref name="from"/> to the matching bracket, or null for a call that runs on past the line's end.</summary>
    private static List<string>? Arguments(string text, int from)
    {
        var found = new List<string>();
        var start = from;
        var depth = 0;
        var quoted = false;
        for (var index = from; index < text.Length; index++)
        {
            var letter = text[index];
            if (letter == '"' && (index == 0 || text[index - 1] != '\\'))
            {
                quoted = !quoted;
            }
            else if (!quoted && letter is '(' or '[')
            {
                depth++;
            }
            else if (!quoted && letter is ')' or ']')
            {
                if (depth == 0)
                {
                    found.Add(text[start..index].Trim());
                    return found;
                }
                depth--;
            }
            else if (!quoted && letter == ',' && depth == 0)
            {
                found.Add(text[start..index].Trim());
                start = index + 1;
            }
        }
        return null;
    }
}

public sealed class ChangesTests
{
    [Fact]
    public void ABurstPostsOnceAndTheReaderGetsEverything()
    {
        var posts = new List<int>();
        var changes = new Changes(posts.Add);
        changes.Add(1);
        changes.Add(2);
        changes.Add(1);
        Assert.Equal([1], posts);
        Assert.Equal(3, changes.Take());
        Assert.Equal(0, changes.Take());
        changes.Add(4);
        Assert.Equal([1, 4], posts);
    }

    [Fact]
    public void ManyThreadsStillPostOncePerRead()
    {
        var posts = 0;
        var changes = new Changes(_ => Interlocked.Increment(ref posts));
        Parallel.For(0, 1000, i => changes.Add(1 << (i % 5)));
        Assert.Equal(1, posts);
        Assert.Equal(31, changes.Take());
    }
}
