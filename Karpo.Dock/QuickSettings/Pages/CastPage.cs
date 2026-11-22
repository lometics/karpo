// Windows' Cast panel as a line of a list folder (QuickPageTile, docs\widgets.md §114): "Cast", the
// wireless displays in range, the one this PC is showing on first and marked "Connected", in the
// quick settings' list. It was the cast widget's flyout, then (§104) only Win+K; the widget is the
// cast preset now, this line over Win+P's modes (ProjectPage) and the cast actions.
//
// Windows lets no unpackaged app connect to a wireless display or disconnect from one
// (Services\Cast.cs says what was tried and why it can't be), so that is Windows' own cast panel's:
// a display's row opens it, as does "Open Cast" under the list, and the line under the list says so.
// Win+K is pressed while the folder still has the keys; the panel takes the focus, and the folder
// goes as it goes for any window that does. "Find other devices" is the actions line's Add device.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class CastPage : QuickPage
{
    private static readonly string Rune = char.ConvertFromUtf32(0xEC15);

    // Segoe Fluent Icons: TVMonitor, a display's row.
    private static readonly string Screen = char.ConvertFromUtf32(0xE7F4);

    private readonly ICastSource source;
    private readonly Label title = new(S.DockQuickCastTitle, LabelStyle.Strong);
    private readonly Label problem = new("", LabelStyle.Critical, lines: 3);
    private readonly Label how = new(S.DockQuickCastHow, LabelStyle.Caption, lines: 3);
    private readonly Notice notice = new(Rune);
    private readonly Progress looking = new(S.DockQuickCastLooking);
    private readonly Button open;
    private readonly Keyed<ListRow> rows = new();
    private CastState state = new(true, []);

    public CastPage(IQuickHost host, ICastSource source)
        : base(host)
    {
        this.source = source;
        open = new Button(S.DockQuickCastOpen, ButtonStyle.Standard, Rune) { Name = "Open Cast", Clicked = Open }; // not-ui: the automation id
    }

    public override QuickKind Kind => QuickKind.Cast;

    /// <summary>Windows' cast panel, where a display is connected and disconnected.</summary>
    private void Open()
    {
        Host.Close();
        source.OpenCast();
    }

    public override void Start()
    {
        source.Start(Changes);
        state = source.Read();
        Host.Rebuild();
    }

    protected override void Refresh(int news) => state = source.Read();

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        layout.Title(title);
        if (state.Problem is { Length: > 0 } trouble)
        {
            problem.Text = trouble;
            layout.Gap(4);
            Text(layout, problem, layout.TextLeft, layout.TextRight);
            layout.Gap(8);
        }
        foreach (var display in state.Displays)
        {
            var row = rows.Get(display.Id, () => new ListRow { Name = display.Name });
            row.Title = display.Name;
            row.Detail = display.Connected ? S.DockQuickConnected : S.DockQuickCastAvailable;
            row.Strong = display.Connected;
            row.Selected = display.Connected;
            row.Glyph = Screen;
            row.Clicked = Open;
            layout.Row(row, context.T(56));
            layout.Gap(2);
        }
        rows.Done();
        if (state.Displays.Count == 0)
        {
            if (state.Looking)
            {
                layout.Place(looking, layout.TextLeft, layout.TextRight - layout.TextLeft, context.T(32));
            }
            else
            {
                Notice(layout, notice.Say(Rune, S.DockQuickCastNone, S.DockQuickCastNoneDetail));
            }
        }
        layout.Gap(4);
        Text(layout, how, layout.TextLeft, layout.TextRight);
        layout.Gap(8);
        layout.Place(open, layout.TextLeft, MathF.Min(open.NaturalWidth(context), layout.TextRight - layout.TextLeft), context.T(32));
        layout.Gap(4);
    }

    public override void Dispose() => source.Dispose();
}
