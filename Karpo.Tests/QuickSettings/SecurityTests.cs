// The Security widget's judgement, and a picture of every state it can be in.
//
// The mapping is where the care is. Three of the four states cannot be produced on the machine
// running these tests: every provider on a healthy PC reports GOOD, and the way to a POOR reading
// is to turn somebody's antivirus off, which is not a thing to do to a person's machine for a
// test. So the rules are tested as rules, over every combination Security Center can report, and
// the drawing is checked by a picture per state with a made-up source behind it.
//
// Nothing here reads the real antivirus: Security.Source hands any isolated run a made-up one
// (docs\widgets.md section 11), and the pictures set a source of their own besides.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class SecurityTests
{
    private static readonly ProviderHealth[] Every =
        [ProviderHealth.Unknown, ProviderHealth.Good, ProviderHealth.NotMonitored, ProviderHealth.Poor, ProviderHealth.Snoozed];

    /// <summary>
    /// Nothing known about either provider means nothing said about both. This is the rule the
    /// widget exists to keep: a shield that says "protected" before Windows has answered is the
    /// worst thing it could do, so Unknown beats every other answer rather than being averaged
    /// with it.
    /// </summary>
    [Fact]
    public void NotKnowingBeatsEverything()
    {
        foreach (var other in Every)
        {
            Assert.Equal(SecurityStanding.Unknown, Security.StandingOf(ProviderHealth.Unknown, other));
            Assert.Equal(SecurityStanding.Unknown, Security.StandingOf(other, ProviderHealth.Unknown));
        }
        Assert.Equal(SecurityStanding.Unknown, SecurityState.Nothing.Standing);
    }

    /// <summary>
    /// Every combination Security Center can report, and what the widget makes of it. The worst
    /// answer wins, and "not monitored" is not a worst answer — Windows declining to watch a
    /// provider is neither good nor bad, and drawing it as trouble is how a shield becomes a thing
    /// people learn to ignore.
    /// </summary>
    [Fact]
    public void TheWorstAnswerWinsAndNotWatchedIsNotAWorstAnswer()
    {
        foreach (var antivirus in Every)
        {
            foreach (var firewall in Every)
            {
                var standing = Security.StandingOf(antivirus, firewall);
                var wanted =
                    antivirus == ProviderHealth.Unknown || firewall == ProviderHealth.Unknown ? SecurityStanding.Unknown
                    : Wants(antivirus) || Wants(firewall) ? SecurityStanding.Attention
                    : antivirus == ProviderHealth.NotMonitored || firewall == ProviderHealth.NotMonitored ? SecurityStanding.NotMonitored
                    : SecurityStanding.Good;
                Assert.Equal(wanted, standing);
            }
        }
        // Spelled out, so the table above cannot quietly agree with itself.
        Assert.Equal(SecurityStanding.Good, Security.StandingOf(ProviderHealth.Good, ProviderHealth.Good));
        Assert.Equal(SecurityStanding.Attention, Security.StandingOf(ProviderHealth.Poor, ProviderHealth.Good));
        Assert.Equal(SecurityStanding.Attention, Security.StandingOf(ProviderHealth.Good, ProviderHealth.Snoozed));
        Assert.Equal(SecurityStanding.Attention, Security.StandingOf(ProviderHealth.Poor, ProviderHealth.NotMonitored));
        Assert.Equal(SecurityStanding.NotMonitored, Security.StandingOf(ProviderHealth.NotMonitored, ProviderHealth.Good));

        static bool Wants(ProviderHealth health) => health is ProviderHealth.Poor or ProviderHealth.Snoozed;
    }

    /// <summary>
    /// Somebody else in charge: the widget names them and never claims to be Defender. This is the
    /// whole reason it is called Security, so it is worth a test rather than a comment.
    /// </summary>
    [Fact]
    public void SomebodyElseInChargeIsNamedAndDefenderIsNotClaimed()
    {
        var other = new SecurityState(
            ProviderHealth.Good, ProviderHealth.Good, "Norton 360", IsDefender: false, Security.WindowsSecurity);
        var said = Security.Say(other);
        Assert.Contains("Norton 360 is in charge.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Defender", said, StringComparison.OrdinalIgnoreCase);
        // And it still says the machine is protected, because it is.
        Assert.Equal(SecurityStanding.Good, other.Standing);
        Assert.Contains("this PC is protected", said, StringComparison.Ordinal);
    }

    /// <summary>Nothing registered is its own answer, and not the same as not having looked.</summary>
    [Fact]
    public void NoAntivirusAtAllSaysSoAndSayingNothingYetSaysThat()
    {
        var none = new SecurityState(ProviderHealth.Poor, ProviderHealth.Good, Guard: null);
        Assert.Contains("No antivirus is registered", Security.Say(none), StringComparison.Ordinal);

        var nothing = Security.Say(SecurityState.Nothing);
        Assert.Contains("hasn't answered yet", nothing, StringComparison.Ordinal);
        // Blank, never wrong: before anything is known it must not say protected or unprotected.
        Assert.DoesNotContain("protected", nothing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attention", nothing, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every tooltip says what a click does, because a click is the whole of what it offers.</summary>
    [Fact]
    public void TheTooltipAlwaysSaysWhereAClickGoes()
    {
        foreach (var antivirus in Every)
        {
            foreach (var firewall in Every)
            {
                Assert.EndsWith(
                    "A click opens Windows Security.",
                    Security.Say(new SecurityState(antivirus, firewall, "Something")),
                    StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Security Center hands over whatever a product registered as its remediation path, and for a
    /// third-party product that can be a path to an executable. Karpo will not run one because a
    /// registry field said so: only a URI scheme is taken.
    /// </summary>
    [Theory]
    [InlineData("windowsdefender://", "windowsdefender://")]
    [InlineData("ms-settings:windowsdefender", "ms-settings:windowsdefender")]
    [InlineData("norton-vault:open", "norton-vault:open")]
    [InlineData(@"C:\Program Files\Something\Remediate.exe", "windowsdefender://")]
    [InlineData(@"\\server\share\Remediate.exe", "windowsdefender://")]
    [InlineData("Remediate.exe", "windowsdefender://")]
    [InlineData(@"%ProgramFiles%\Something\x.exe", "windowsdefender://")]
    [InlineData("some program:with a space", "windowsdefender://")]
    [InlineData(":nothing", "windowsdefender://")]
    [InlineData("", "windowsdefender://")]
    [InlineData(null, "windowsdefender://")]
    public void OnlyAUriIsFollowed(string? remediation, string expected) =>
        Assert.Equal(expected, Security.OpenFor(remediation));

    /// <summary>
    /// The glyph and the accent, per state. A warning gets its own glyph rather than a red shield,
    /// because a red shield and a green shield are the same shape to somebody who cannot tell the
    /// colours apart; and only Good lights up, so no accent ever means "not protected".
    /// </summary>
    [Fact]
    public void OnlyProtectedLightsUpAndTroubleHasItsOwnGlyph()
    {
        Assert.True(SecurityIsland.LitFor(SecurityStanding.Good));
        Assert.False(SecurityIsland.LitFor(SecurityStanding.Unknown));
        Assert.False(SecurityIsland.LitFor(SecurityStanding.Attention));
        Assert.False(SecurityIsland.LitFor(SecurityStanding.NotMonitored));

        Assert.Equal(SecurityIsland.AttentionGlyph, SecurityIsland.GlyphFor(SecurityStanding.Attention));
        foreach (var standing in (SecurityStanding[])[SecurityStanding.Unknown, SecurityStanding.Good, SecurityStanding.NotMonitored])
        {
            Assert.Equal(SecurityIsland.ShieldGlyph, SecurityIsland.GlyphFor(standing));
        }
    }

    /// <summary>A card's line says where it stands in as few words as it takes, and never nothing.</summary>
    [Fact]
    public void ACardsLineSaysWhereItStands()
    {
        Assert.Equal("Protected", Security.Line(new SecurityState(ProviderHealth.Good, ProviderHealth.Good)));
        Assert.Equal("Needs attention", Security.Line(new SecurityState(ProviderHealth.Poor, ProviderHealth.Good)));
        Assert.Equal("Not watched", Security.Line(new SecurityState(ProviderHealth.NotMonitored, ProviderHealth.Good)));
        Assert.Equal("Asking Windows", Security.Line(SecurityState.Nothing));
    }

    /// <summary>
    /// A run that may not reach the person's world gets a source that reaches nothing. The
    /// guarantee is in Security.Source, not in the widget and not in each drawing path, for the
    /// reason the switches learned it: a drawn widget is what every offscreen picture produces.
    /// </summary>
    [Fact]
    public void AnIsolatedRunReadsNobodysAntivirus()
    {
        Assert.True(Config.IsIsolated, "The tests are meant to run on a config of their own.");
        using var source = Security.Source();
        Assert.IsType<SampleSecurity>(source);
    }

    /// <summary>Making the widget asks Windows nothing; the first look does (docs\widgets.md section 11).</summary>
    [UiFact]
    public void MakingTheWidgetAsksWindowsNothingUntilSomebodyLooks()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var started = 0;
            Security.Made = () => new CountedSecurity(() => started++);
            try
            {
                using var island = Island(gfx);
                Assert.Equal(0, started);
                // Nothing known, so nothing claimed.
                Assert.Equal(SecurityStanding.Unknown, island.State.Standing);
                _ = island.Tooltip(0);
                Assert.Equal(1, started);
                _ = island.Tooltip(0);
                Assert.Equal(1, started);
            }
            finally
            {
                Security.Made = null;
            }
        });
    }

    /// <summary>
    /// A picture of the widget in every state it can be in, on a bar, light and dark, with the
    /// name beside it and without. Three of the four cannot be read off this machine, so a made-up
    /// source stands in for them — which is the only honest way to see what a poor reading draws
    /// without turning somebody's antivirus off.
    /// </summary>
    [UiFact]
    public void APictureOfEveryState()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (dark, name) in ((bool Dark, string Name)[])[(true, "dark"), (false, "light")])
            {
                foreach (var label in (bool[])[false, true])
                {
                    Sheet(gfx, LookFor(dark), label, $"security-{name}{(label ? "-named" : "")}.png");
                }
            }
        });
    }

    /// <summary>The widget's ink stays inside its cell in every state: no state paints outside what it measured.</summary>
    [UiFact]
    public void NoStatePaintsOutsideItsCell()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = Style(LookFor(dark: true), Thickness);
            float? width = null;
            foreach (var state in States)
            {
                Security.Made = () => new SampleSecurity(state);
                try
                {
                    using var island = Island(gfx);
                    var cell = MathF.Ceiling(Length(island, style));
                    // The size is a function of the options, so every state measures the same:
                    // the state arriving must never move the bar.
                    width ??= cell;
                    Assert.Equal(width, cell);
                    (float Left, float Top, float Right, float Bottom) ink = default;
                    Offscreen.Bitmap(
                        cell,
                        Thickness,
                        dc => island.PaintCell(dc, 0, style, cell, Thickness),
                        bitmap => ink = Offscreen.InkOf(bitmap, (uint)cell, (uint)Thickness));
                    Assert.True(ink.Right > ink.Left, $"{state.Standing} drew nothing at all.");
                    Assert.InRange(ink.Left, 0, cell);
                    Assert.InRange(ink.Right, 0, cell);
                }
                finally
                {
                    Security.Made = null;
                }
            }
        });
    }

    // --- The made-up PCs the pictures are of ------------------------------------

    private const float Thickness = 48;

    private static readonly SecurityState[] States =
    [
        SecurityState.Nothing,
        new(ProviderHealth.Good, ProviderHealth.Good, "Microsoft Defender Antivirus", IsDefender: true, Security.WindowsSecurity),
        new(ProviderHealth.Poor, ProviderHealth.Good, "Microsoft Defender Antivirus", IsDefender: true, Security.WindowsSecurity),
        new(ProviderHealth.NotMonitored, ProviderHealth.Good, "Norton 360", IsDefender: false, Security.WindowsSecurity),
    ];

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static IslandStyle Style(Look look, float thickness) =>
        new(1f, false, thickness, MathF.Round(thickness / 2 / 2) * 2, look);

    /// <summary>
    /// A widget, whose source is whatever Security.Made is set to <b>while it is made</b>: the
    /// island asks for its source in its constructor, and Made is per thread, so it has to be set
    /// on this one and around this call.
    /// </summary>
    private static SecurityIsland Island(Gfx gfx, bool label = false)
    {
        var settings = BarKind.Security.New();
        settings.Id = "security";
        settings.Switch = new SwitchOptions { ShowLabel = label };
        return new SecurityIsland($"layout:default/{settings.Id}", settings, gfx, new Config());
    }

    private static float Length(SecurityIsland island, IslandStyle style)
    {
        var cells = island.Measure(style);
        return cells.Lengths is { Count: > 0 } lengths ? lengths[0] : cells.CellLength;
    }

    private static void Sheet(Gfx gfx, Look look, bool label, string file)
    {
        var style = Style(look, Thickness);
        var islands = new List<SecurityIsland>();
        try
        {
            foreach (var state in States)
            {
                Security.Made = () => new SampleSecurity(state);
                try
                {
                    islands.Add(Island(gfx, label));
                }
                finally
                {
                    Security.Made = null;
                }
            }
            var widths = islands.Select(island => MathF.Ceiling(Length(island, style))).ToList();
            const float gap = 12;
            var width = widths.Sum() + (gap * (widths.Count - 1));
            Offscreen.Bitmap(
                width,
                Thickness,
                dc =>
                {
                    var background = look.Dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.96f, 0.96f, 0.96f, 1);
                    dc->Clear(&background);
                    var at = 0f;
                    for (var index = 0; index < islands.Count; index++)
                    {
                        var cell = widths[index];
                        var drawn = dc;
                        var island = islands[index];
                        Offscreen.Shifted(dc, at, 0, () => island.PaintCell(drawn, 0, style, cell, Thickness));
                        at += cell + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var island in islands)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>A source that counts how often it was asked to start.</summary>
    private sealed class CountedSecurity(Action started) : ISecuritySource
    {
        public void Start(Action changed) => started();

        public SecurityState Read() => SecurityState.Nothing;

        public void Dispose()
        {
        }
    }
}
