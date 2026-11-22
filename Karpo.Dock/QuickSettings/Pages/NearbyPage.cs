// Windows' Nearby sharing as a line of a list folder (QuickPageTile, docs\widgets.md §115): the
// nearby sharing preset's. The person, 2026-10-11: "sharing widget just opens the sharing settings,
// is it possible to actually make a proper flyout out of this?" So what Settings > System > Nearby
// sharing holds, in the quick settings' style:
//
//   "Nearby sharing", and what it is: sharing with devices nearby over Bluetooth and Wi-Fi.
//   Off, My devices only, Everyone nearby: the one in force marked as a quick-settings list marks
//     its chosen row, a click putting another in force (Services\Nearby.cs, off the dock's thread).
//   "Save files I receive to": the folder, and Change, which asks in Windows' folder dialog.
//   "Discoverable as": the name other devices find this PC by.
//
// The mode asked for is marked at once and the mark follows what Windows then says; where the
// change did not take, the line says so under the modes. "Nearby sharing settings" is the actions
// line under it.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class NearbyPage : QuickPage
{
    private readonly INearbySource source;
    private readonly Label title = new(S.DockQuickNearbyTitle, LabelStyle.Strong);
    private readonly Label caption = new(S.DockQuickNearbyCaption, LabelStyle.Caption, lines: 3);
    private readonly Label problem = new("", LabelStyle.Critical, lines: 2);
    private readonly (Label Caption, Label Value) saveTo = (new Label(S.DockQuickNearbySaveTo, LabelStyle.Caption), new Label("", LabelStyle.Body));
    private readonly (Label Caption, Label Value) device = (new Label(S.DockQuickNearbyDevice, LabelStyle.Caption), new Label("", LabelStyle.Body));
    private readonly Button change;
    private readonly Dictionary<NearbyMode, ListRow> rows = [];
    private NearbyState state = new(null, "", "");

    // The mode asked for and not yet in force (Windows is asked off this thread).
    private NearbyMode? asked;

    public NearbyPage(IQuickHost host, INearbySource source)
        : base(host)
    {
        this.source = source;
        foreach (var mode in NearbyModes.All)
        {
            var row = new ListRow { Name = NearbyModes.Title(mode), Title = NearbyModes.Title(mode), Detail = NearbyModes.Detail(mode), Glyph = NearbyModes.Glyph(mode) };
            row.Clicked = () => Choose(mode);
            rows[mode] = row;
        }
        change = new Button(S.DockQuickNearbyChange, ButtonStyle.Standard) { Name = S.DockQuickNearbyChange, Clicked = source.ChooseFolder };
    }

    public override QuickKind Kind => QuickKind.Nearby;

    /// <summary>The mode the line marks: the one asked for until Windows has answered, else the one in force.</summary>
    internal NearbyMode? Marked => asked ?? state.Mode;

    public override void Start()
    {
        source.Start(Changes);
        state = source.Read();
        Host.Rebuild();
    }

    protected override void Refresh(int news)
    {
        state = source.Read();
        // Windows has answered: the mode asked for is in force, or it said why not.
        if (asked is { } waiting && (state.Mode == waiting || state.Problem is not null))
        {
            asked = null;
        }
    }

    private void Choose(NearbyMode mode)
    {
        if (Marked == mode)
        {
            return;
        }
        asked = mode;
        source.Set(mode);
        Host.Rebuild();
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        layout.Title(title);
        Text(layout, caption, layout.TextLeft, layout.TextRight);
        layout.Gap(6);
        var marked = Marked;
        foreach (var mode in NearbyModes.All)
        {
            var row = rows[mode];
            row.Selected = marked == mode;
            layout.Row(row, context.T(52));
            layout.Gap(2);
        }
        if (state.Problem is { Length: > 0 } trouble)
        {
            problem.Text = trouble;
            layout.Gap(4);
            Text(layout, problem, layout.TextLeft, layout.TextRight);
        }
        layout.Gap(4);
        // Where received files go, and Change beside it, as Settings has them.
        var top = layout.Y;
        var buttonWidth = MathF.Min(change.NaturalWidth(context), (layout.TextRight - layout.TextLeft) / 2);
        // On a line, against the trailing gutter as whatever ends a row (§401); in the flyout, 4 in from its rows' end.
        var buttonLeft = layout.RowRight - (layout.Lined ? 0 : context.D(4)) - buttonWidth;
        Property(layout, saveTo, state.SaveTo, buttonLeft - context.D(8));
        var height = context.T(32);
        layout.Put(change, PopupPaint.Rect(buttonLeft, PopupPaint.Round(top + ((layout.Y - top - height) / 2)), buttonWidth, height));
        if (state.Device.Length > 0)
        {
            Property(layout, device, state.Device);
        }
    }

    /// <summary>One property: what it is in the caption's ink, and its value under it, up to <paramref name="right"/>.</summary>
    private static void Property(PageLayout layout, (Label Caption, Label Value) pair, string value, float? right = null)
    {
        var context = layout.Context;
        var width = MathF.Max(1, (right ?? layout.TextRight) - layout.TextLeft);
        pair.Value.Text = value;
        layout.Gap(4);
        layout.Place(pair.Caption, layout.TextLeft, width, context.T(16));
        layout.Place(pair.Value, layout.TextLeft, width, context.T(22));
        layout.Gap(4);
    }

    public override void Dispose() => source.Dispose();
}
