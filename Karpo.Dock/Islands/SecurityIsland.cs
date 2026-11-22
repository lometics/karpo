// The Security widget: whether this PC is protected, as one glyph on the bar.
//
// A shield on the accent while the antivirus and the firewall are both good, a
// warning where one of them wants attention, and a quiet shield where Windows is
// not watching or has not answered yet. A click opens Windows Security and does
// nothing else — see the top of QuickSettings\Services\Security.cs for why it is
// called Security rather than Defender, what is watched and what is not, and why
// none of it comes from WMI.
//
// BLANK, NEVER WRONG. Until Windows has answered, the widget shows no state: the
// shield in the quiet ink, no accent behind it, and a tooltip that says nobody
// has answered yet. A shield that says "protected" before it has looked would be
// the worst thing this widget could do, and it is the reason Unknown beats every
// other answer in Security.StandingOf rather than being folded into one of them.
//
// In a list folder it is a line of its own (ListForm), laid out for the height it is given as every
// list line is (the person, 2026-09-27: "the protected should show more information if it is higher
// and less if it is less high"; docs\widgets.md §9, "A line at every height"): the glyph in the
// list's leading column, on the accent while the machine is protected; on one row the antivirus's
// name and where the machine stands against the trailing gutter; taller, where it stands over who is
// in charge, and both protections under that; and on the whole widget each protection with what
// Windows says of it (Line, ListTiers).
//
// Nothing here is read on a timer. Windows' state is followed from the dock's
// start by the one security source (Sources\DeviceData.cs, SecurityData), for
// every security widget the config names, so a folder that opens shows where the
// PC stands at once; the source asks Windows to call back rather than polling.
// A widget a test hands a stand-in of its own (Security.Made) starts it the first
// time it is drawn, hovered or pressed (Follow; docs\widgets.md section 11).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe partial class SecurityIsland : CellIsland, IListContent, ILinePieces
{
    /// <summary>A shield: what the widget is, whatever it happens to be saying.</summary>
    internal const string ShieldGlyph = "\uEA18";

    /// <summary>The warning triangle, for a state that wants the person.</summary>
    internal const string AttentionGlyph = "\uE7BA";

    private readonly ISecuritySource source;
    private readonly DataSources sources;

    // The one source every security widget reads (SecurityData), started at the dock's start; null
    // for a widget a test has handed a stand-in of its own (Security.Made), which it starts itself.
    private readonly SecurityData? data;
    private SwitchOptions options;
    private bool following;

    public SecurityIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, DataSources.Shared)
    {
    }

    internal SecurityIsland(string key, WidgetItem island, Gfx gfx, Config config, DataSources sources)
        : base(key, island, gfx)
    {
        options = island.Switch ?? new SwitchOptions();
        this.sources = sources;
        var held = Security.Made is null ? sources.Hold(island) : null;
        if (held is SecurityData shared)
        {
            data = shared;
            source = shared.Windows;
            // Followed since the source started: what Windows said is there to read at once.
            following = true;
            shared.Listen(Post);
        }
        else
        {
            sources.Release(held);
            source = Security.Source();
        }
    }

    /// <summary>Nothing ticks: Windows calls back when its security state changes.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>What Windows last said, or nothing at all before anybody has looked.</summary>
    internal SecurityState State => following ? source.Read() : SecurityState.Nothing;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the settings just
        // handed over (see SwitchIsland.Configure for the bug this rule came from).
        Settings = island;
        options = island.Switch ?? new SwitchOptions();
    }

    /// <summary>
    /// Starts following Windows' security state the first time anybody looks at a widget with a
    /// stand-in of its own (a test's). The dock's widgets read the one source that has followed it
    /// since the dock started (SecurityData), and this does nothing for them.
    /// </summary>
    private void Follow()
    {
        if (following)
        {
            return;
        }
        following = true;
        source.Start(Post);
    }

    public override string? Tooltip(int index)
    {
        Follow();
        return Security.Say(source.Read());
    }

    /// <summary>
    /// Opens Windows Security, which is the whole of what a click may do. Karpo never switches
    /// protection, starts a scan or dismisses a warning: those are Windows' to offer.
    /// </summary>
    public override IslandAction Click(int index)
    {
        Follow();
        Launch.Open(source.Read().Open ?? Security.WindowsSecurity);
        return default;
    }

    protected override bool Sample(bool everything) => false;

    public override void Dispose()
    {
        if (data is not null)
        {
            data.Unlisten(Post);
            sources.Release(data);
        }
        else
        {
            source.Dispose();
        }
        base.Dispose();
    }

    /// <summary>
    /// The glyph for a standing. A warning is its own glyph rather than a coloured shield, because
    /// a red shield and a green shield are the same shape to somebody who cannot tell them apart.
    /// </summary>
    internal static string GlyphFor(SecurityStanding standing) =>
        standing == SecurityStanding.Attention ? AttentionGlyph : ShieldGlyph;

    /// <summary>
    /// Whether the accent goes behind the glyph. Only for Good: neither "not watched" nor "not
    /// answered yet" is a state to light up, and neither is a state to draw as trouble either.
    /// </summary>
    internal static bool LitFor(SecurityStanding standing) => standing == SecurityStanding.Good;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        Follow();
        var ink = new IslandInk(style.Look);
        var standing = source.Read().Standing;
        var lit = LitFor(standing);
        var color = lit
            ? SwitchIsland.OnAccent(style.Look)
            : standing switch
            {
                SecurityStanding.Attention => ink.Alarm,
                // Nothing said yet, and nothing said is not "off": the quiet ink, not the
                // disabled one, which the bar uses for a thing this PC has not got.
                SecurityStanding.Unknown => ink.Secondary,
                _ => ink.Text,
            };
        // The kind's own title, which is what the shape measured room for — never what the widget
        // happens to be saying, which would move the bar as the state arrived. Lit, the accent fills
        // the whole button, as a switch's does (§648).
        SwitchIsland.PaintButton(dc, Gfx, style, width, height, options.ShowLabel, GlyphFor(standing), color, Info.Title, lit ? color : ink.Text, lit ? ink.Accent : null);
    }

    // --- In a folder ------------------------------------------------------------

    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(new LineSketch(dc, Gfx), box, style);

    /// <summary>The glyph's ink for a standing off the accent: trouble in the alarm's, nothing said yet in the quiet ink.</summary>
    private static DXGI_RGBA Ink(SecurityStanding standing, FlyoutTheme theme) => standing switch
    {
        SecurityStanding.Attention => Alarm(theme),
        SecurityStanding.Unknown => theme.TextSecondary,
        _ => theme.Text,
    };

    // --- As a line of a list ----------------------------------------------------

    /// <summary>What a line of one row shows first: where the machine stands.</summary>
    public string MainPiece => "standing";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style);
        return sketch.Pieces;
    }

    /// <summary>
    /// Its form for its size (§432): where the machine stands (Compact); over who is in charge, the kit's row
    /// with its caption (Medium, where a new line starts); and a row for each protection under it (Full).
    /// </summary>
    public float ListHeight(float width, in TileStyle style) => LineSizes.Of(Settings) switch
    {
        LineSize.Compact => LineRow.Height(style, caption: false),
        LineSize.Medium => LineRow.Height(style, caption: true),
        _ => LineRow.Height(style, caption: true) + (2 * LineRow.Height(style, caption: false)),
    };

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), on the
    /// kit's rows (§401), more of what Windows says the taller it is, never larger:
    /// <list type="bullet">
    /// <item>One row: the glyph, in the accent while the machine is protected, and where it stands.</item>
    /// <item>From a row and its caption: who is in charge under where it stands.</item>
    /// <item>Taller: a row for each protection, what Windows says of it against the trailing gutter.</item>
    /// </list>
    /// Nothing says a state Windows has not given: before it answers the line is its standing, "Asking
    /// Windows", and who is in charge says it has not answered yet.
    /// </summary>
    private void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style)
    {
        Follow();
        var state = source.Read();
        var standing = state.Standing;
        var height = box.bottom - box.top;
        var captioned = LineRow.Height(style, caption: true);
        var row = new RowSpec(Security.Line(state))
        {
            Glyph = GlyphFor(standing),
            GlyphInk = LitFor(standing) ? PopupPaint.Ink(style.Theme.Accent) : Ink(standing, style.Theme),
            Caption = height + 0.5f >= captioned ? Security.Guard(state) : null,
            NamePiece = "standing",
            CaptionPiece = "guard",
        };
        var laid = LineRow.Draw(sketch, box.left, box.right, box.top, style, row, row.Caption is null ? height : 0);
        var each = LineRow.Height(style, caption: false);
        if (standing == SecurityStanding.Unknown || laid.Bottom + (2 * each) > box.bottom + 0.5f)
        {
            return;
        }
        Protection(sketch, style, box, "antivirus", S.WidgetSecurityVirusProtection, state.Antivirus, laid.Bottom);
        Protection(sketch, style, box, "firewall", S.WidgetSecurityFirewallProtection, state.Firewall, laid.Bottom + each);
    }

    /// <summary>One protection on a row of its own: its glyph, its name, and what Windows says of it against the trailing gutter.</summary>
    private static void Protection(LineSketch sketch, in TileStyle style, D2D_RECT_F box, string name, string label, ProviderHealth health, float top)
    {
        var theme = style.Theme;
        var (glyph, ink) = health switch
        {
            ProviderHealth.Good => ("", PopupPaint.Ink(theme.Accent)),
            ProviderHealth.Poor or ProviderHealth.Snoozed => (AttentionGlyph, Alarm(theme)),
            _ => ("", theme.TextSecondary),
        };
        var word = Word(health);
        LineRow.Draw(sketch, box.left, box.right, top, style, new RowSpec(label)
        {
            Glyph = glyph,
            GlyphInk = ink,
            Reading = char.ToUpperInvariant(word[0]) + word[1..],
            Look = RowReading.Body,
            ReadingInk = theme.TextSecondary,
            GlyphPiece = $"{name} glyph",
            NamePiece = name,
            ReadingPiece = $"{name} state",
        });
    }

    /// <summary>What Windows says of one protection, in a word or two.</summary>
    private static string Word(ProviderHealth health) => health switch
    {
        ProviderHealth.Good => S.WidgetSecurityOn,
        ProviderHealth.Poor => S.WidgetSecurityNeedsAttention,
        ProviderHealth.NotMonitored => S.WidgetSecurityNotMonitored,
        ProviderHealth.Snoozed => S.WidgetSecuritySnoozed,
        _ => S.WidgetSecurityNotAnswered,
    };

    /// <summary>
    /// The ink for something wanting attention, as a nearly full drive's bar uses (DriveIsland):
    /// one colour for trouble across the app, and the system's own highlight in high contrast,
    /// where a made-up red would be the one thing on screen not honouring the person's choice.
    /// </summary>
    private static DXGI_RGBA Alarm(FlyoutTheme theme) =>
        theme.HighContrast ? PopupPaint.Ink(theme.Accent) : theme.Dark ? Gfx.Ink(1f, 0.6f, 0.643f, 1f) : Gfx.Ink(0.769f, 0.169f, 0.11f, 1f);
}
