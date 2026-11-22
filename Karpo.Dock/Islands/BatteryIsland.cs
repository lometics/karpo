// The battery as a line of a list folder (ListForm): the level's glyph in the list's leading
// column, the percentage and what the battery is doing on the first row, the charge as a bar under
// them, and how long it has left — the battery flyout's own words (BatteryText.TimeText), read from
// the battery's source, because the status reading has no time in it.
//
// It was a taskbar entry too, the level glyph and the percentage, whose click opened Karpo's
// battery flyout; that is the battery preset now (2026-10-10, docs\widgets.md §94): a folder
// wearing the battery face (BatteryFaceDrawing, the entry's drawing) and holding this line, energy
// saver's switch and the power mode's line, as the flyout held them.
//
// quick: a PC with no battery has no charge to show, so the line shows a full battery on the wall,
// as it would be, greyed, taking no click, its tooltip saying there is none (CellIsland.UnavailableOn,
// docs\widgets.md §247; the person, 2026-10-19: "show contents as if there is a battery available
// but just show them greyed out").

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using System.Globalization;
using System.Numerics;

namespace Karpo.Dock;

internal sealed unsafe partial class BatteryIsland : CellIsland, IListContent
{
    private SystemStatus.Battery? battery;

    /// <summary>
    /// The battery's source, for the one thing the status reading lacks: how long is left. Made the
    /// first time the line is drawn or measured (docs\widgets.md §11).
    /// </summary>
    private IBatterySource? times;

    [ThreadStatic]
    private static Func<IBatterySource>? made;

    /// <summary>
    /// A test's own battery source, per thread: the line's time left is then that source's. Unset,
    /// it is the samples on a run that may not reach the person's world, and Windows' own otherwise.
    /// </summary>
    internal static Func<IBatterySource>? Made
    {
        get => made;
        set => made = value;
    }

    // Per thread, so a test that fakes a charge on its own thread can't be seen by another test
    // (or by the dock) running beside it; unset, it is this PC's own reading.
    [ThreadStatic]
    private static Func<SystemStatus.Battery?>? probe;

    /// <summary>
    /// Where the charge is read from. This PC's, except in the tests, which set it so the pictures
    /// show a PC with a battery and one without whatever the machine drawing them has.
    /// </summary>
    internal static Func<SystemStatus.Battery?> Probe
    {
        get => probe ?? SystemStatus.ReadBatteryNow;
        set => probe = value;
    }

    public BatteryIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        battery = Probe();
    }

    /// <summary>
    /// perf: no ticking without a battery, where nothing ever changes; with one, a slow look
    /// behind the power-status broadcasts (see <see cref="System"/>), which Windows sends when the
    /// charger comes or goes and as the charge falls. It was a look every 10 s, on every PC.
    /// </summary>
    public override int IntervalMs => battery is null ? 0 : 60_000;

    protected override int Count => 1;

    /// <summary>
    /// perf: the power status changed (charger, charge), or energy saver did (a power setting: the
    /// dock follows energy saver's own status, which the power status no longer carries since 24H2):
    /// the dock refreshes the island at once.
    /// </summary>
    public override void System(uint msg, TerraFX.Interop.Windows.WPARAM wParam, TerraFX.Interop.Windows.LPARAM lParam)
    {
        const uint PBT_APMPOWERSTATUSCHANGE = 0x000A;
        if (msg == TerraFX.Interop.Windows.WM.WM_POWERBROADCAST && (uint)(nuint)wParam is PBT_APMPOWERSTATUSCHANGE or EnergySaver.PbtPowerSettingChange)
        {
            Post();
        }
    }

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
    }

    /// <summary>What a PC with no battery shows, greyed: a full one on the wall (<see cref="UnavailableOn"/>).</summary>
    internal static readonly SystemStatus.Battery UnavailableCharge = new(100, Plugged: true, Saver: false);

    /// <summary>The charge it shows: the battery's, or <see cref="UnavailableCharge"/> where there is none to read.</summary>
    private SystemStatus.Battery Shown => battery is { Percent: >= 0 } charge ? charge : UnavailableCharge;

    internal override string? UnavailableOn(nint screen) =>
        battery is { Percent: >= 0 } ? null : NotOnThisPc(Info.Title, battery is null ? S.WidgetBatteryNoBattery : null);

    public override string? Tooltip(int index) =>
        battery is { Percent: >= 0 } charge ? SystemStatus.Describe(charge).Tip : UnavailableOn(0);

    /// <summary>A click on the line does nothing (<see cref="CellAct.Nothing"/>): a folder opens no flyout.</summary>
    public override IslandAction Click(int index) => default;

    /// <summary>
    /// folders: energy saver, for the battery's menu in a folder (Dock.WidgetMenus.cs) — the energy
    /// saver widget's own switch (<see cref="QuickSwitches"/>), which turns it on and off as Windows'
    /// tile does and says whether it is. Made and started the first time the menu asks, never for a
    /// widget nobody right-clicks; its own start asks nothing of Windows on this thread.
    /// </summary>
    internal IQuickSwitch Saver
    {
        get
        {
            if (saver is null)
            {
                saver = QuickSwitches.For(QuickSwitch.EnergySaver);
                saver.Start(Post);
            }
            return saver;
        }
    }

    private IQuickSwitch? saver;

    public override void Dispose()
    {
        times?.Dispose();
        times = null;
        saver?.Dispose();
        saver = null;
        base.Dispose();
    }

    /// <summary>Reads the charge, and takes the source's news; the redraw that follows notices a percentage of another shape.</summary>
    protected override bool Sample(bool everything)
    {
        battery = Probe();
        _ = news?.Take();
        return false;
    }

    /// <summary>
    /// Never drawn on a bar: a line of a folder's list and nothing else (<see cref="ListWidgets"/>),
    /// since the battery on the bar became the battery preset's face (BatteryFaceDrawing), so
    /// <see cref="CellIsland.PaintOn"/> refuses a bar drawing before this could be reached.
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- As a line of a list ----------------------------------------------------

    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(dc, box, style);

    /// <summary>
    /// The battery's source, made the first time it is asked for (<see cref="times"/>), and followed
    /// from then: the power mode the tallest line says moves in Windows' own Power &amp; battery page,
    /// a power mode line or a plan, and nothing the dock hears said so — the line kept the old one
    /// until its minute's look (docs\widgets.md §439). The source's news (the effective power mode's
    /// own notification, the charge, the charger) draws it again.
    /// </summary>
    private IBatterySource Times()
    {
        if (times is null)
        {
            times = Made?.Invoke() ?? (Config.IsIsolated || QuickSources.UseSamples ? new SampleBattery() : new WindowsBattery());
            news = new Changes(_ => Post());
            times.Start(news);
        }
        return times;
    }

    /// <summary>What the source said since the line last read it (<see cref="Times"/>), taken as it is drawn.</summary>
    private Changes? news;

    /// <summary>What the battery is doing in a word or two, for a line too narrow for <see cref="StateOf"/>.</summary>
    private static string ShortStateOf(SystemStatus.Battery charge, int percent) =>
        charge.Plugged ? (percent >= 100 ? S.WidgetBatteryCharged : S.WidgetBatteryCharging) : charge.Saver ? S.WidgetBatterySaverOn : S.WidgetBatteryOnBattery;

    /// <summary>What the battery is doing, in the line's words.</summary>
    private static string StateOf(SystemStatus.Battery charge, int percent) =>
        charge.Plugged ? (percent >= 100 ? S.WidgetBatteryFullyCharged : S.WidgetBatteryCharging) : charge.Saver ? S.WidgetBatteryEnergySaverOn : S.WidgetBatteryOnBattery;

    /// <summary>The fill of the charge's bar: the alarm's red at a tenth or less off the wall, else the accent.</summary>
    private static DXGI_RGBA Fill(SystemStatus.Battery charge, int percent, in TileStyle style) =>
        percent <= 10 && !charge.Plugged ? Gfx.Ink(0.769f, 0.169f, 0.11f, 1f) : TilePaint.Accent(style);

    /// <summary>
    /// Its form for its size (§432, §442): the row with the charge as a bar in it (Compact, two cells); the kit's row
    /// with its caption — the name, what the battery is doing and the time left, the percentage — and the charge's
    /// meter under the caption, one block in three cells (Medium, §401); and under that a line each for energy saver
    /// and the power mode, in the middle of two cells more (Full).
    /// </summary>
    public float ListHeight(float width, in TileStyle style) => LineSizes.Of(Settings) switch
    {
        LineSize.Compact => LineRow.Height(style, caption: false),
        LineSize.Medium => LineRow.Height(style, caption: true),
        _ => LineRow.Height(style, caption: true) + LineRows.Px(1, style),
    };

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), on the kit's
    /// blocks (§401), the same pieces in the same places at every height, more of them the taller it is:
    /// <list type="bullet">
    /// <item>One row: the level's glyph in the list's leading column, the widget's name, the charge as a bar in
    /// the room after it, and the percentage against the trailing gutter.</item>
    /// <item>From a row with its caption and the meter: what the battery is doing and the time left under the
    /// name, the charge's meter from the text edge under the row.</item>
    /// <item>Taller: where energy saver and the power mode stand, the battery flyout's own words, a line each
    /// under the meter.</item>
    /// </list>
    /// A PC with no battery shows <see cref="UnavailableCharge"/>, greyed.
    /// </summary>
    private void Line(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(dc, Gfx);
        var form = ListForm.Of(Gfx, style);
        var height = TilePaint.Height(box);
        // No charge to show: a full battery on the wall, which the folder greys (UnavailableOn).
        var charge = Shown;
        var percent = Math.Min(charge.Percent, 100);
        var figure = $"{percent.ToString(CultureInfo.InvariantCulture)}%";
        var fill = Fill(charge, percent, style);
        var glyph = SystemStatus.Describe(charge).Glyph;
        var state = Times().Read();
        var meter = LineMeter.Thick(style);
        if (height + 0.5f < LineRow.Height(style, caption: true))
        {
            var one = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(Info.Title) { Glyph = glyph, Reading = figure }, height);
            var start = (one.Name?.right ?? box.left + form.TextLeft) + style.Px(LineSpace.GapDip);
            var end = (one.Reading?.left ?? box.right - form.Inset) - style.Px(LineSpace.GapDip);
            if (end - start >= ListTiers.LeastPicture(style))
            {
                TilePaint.Bar(dc, PopupPaint.Rect(start, PopupPaint.Round(one.Middle - (meter / 2)), end - start, meter), percent / 100f, TilePaint.Track(style), fill);
            }
            return;
        }
        var row = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(Info.Title)
        {
            Glyph = glyph,
            Caption = LineRow.Join(StateOf(charge, percent), BatteryText.TimeText(state)),
            Reading = figure,
            Under = LineMeter.Room(style),
        });
        LineMeter.Under(sketch, row, box.left, box.right, style, percent / 100f, fill);
        var under = new List<string>();
        if (state.SaverKnown)
        {
            under.Add(state.Saver ? S.WidgetBatteryEnergySaverOn : S.WidgetBatteryEnergySaverOff);
        }
        if (state.Mode is { } mode && PowerModePage.Modes.FirstOrDefault(each => each.Mode == mode).Name is { Length: > 0 } modeName)
        {
            under.Add(S.WidgetBatteryPowerMode(modeName));
        }
        var captionPx = style.Text(LineType.CaptionDip);
        var captionLine = TilePaint.LineHeight(Gfx, captionPx);
        // In the middle of the row of cells under the meter's row (§442).
        var y = row.Bottom + PopupPaint.Round(MathF.Max(0, LineRows.Px(1, style) - (under.Count * captionLine)) / 2);
        foreach (var words in under)
        {
            if (y + captionLine > box.bottom + 0.5f)
            {
                break;
            }
            sketch.Text("more", words, captionPx, Weight.Regular, style.Theme.TextSecondary, box.left + form.TextLeft, y, form.Room(TilePaint.Width(box)));
            y += captionLine;
        }
    }
}
