// The project widget's own flyout: Win+P's four modes as a quick-settings list,
// the one in force marked as the chosen row of any other quick-settings list
// (a subtle fill and the accent pill), and "More display settings" in the
// footer. Choosing one puts it in force through the display configuration, the
// way Win+P does (Services\Projection.cs).
//
// The panel stays up while Windows changes the screens, as Win+P's does, and
// the row chosen is marked at once. When the screens do change the flyout goes
// by itself — a change of displays ends every Menu popup (FlyoutHost) — and
// where Windows refuses, the page stays and says so under the list. With one
// screen attached the four are still offered, as Win+P offers them, with a line
// saying the other three need a second screen.
//
// The same page is the projection line of the cast preset (QuickKind.Projection,
// QuickPageTile, docs\widgets.md §114), where the footer is the actions line's.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class ProjectPage : QuickPage
{
    private readonly IProjectSource source;
    private readonly Label title = new(S.DockQuickProjectTitle, LabelStyle.Strong);
    private readonly Label alone = new(S.DockQuickProjectOneScreen, LabelStyle.Caption, lines: 2);
    private readonly Label problem = new("", LabelStyle.Critical, lines: 3);
    private readonly Dictionary<ProjectMode, ListRow> rows = [];
    private ProjectState state = new(null, 0);

    // The mode chosen here, marked until Windows' answer is read back.
    private ProjectMode? chosen;

    public ProjectPage(IQuickHost host, IProjectSource source)
        : base(host)
    {
        this.source = source;
        foreach (var mode in ProjectModes.All)
        {
            var row = new ListRow { Name = ProjectModes.Title(mode), Title = ProjectModes.Title(mode), Glyph = ProjectModes.Glyph(mode) };
            row.Clicked = () => Choose(mode);
            rows[mode] = row;
        }
    }

    public override QuickKind Kind => QuickKind.Projection;

    /// <summary>The mode the page marks: the one just chosen, else the one Windows says is in force.</summary>
    internal ProjectMode? Marked => chosen ?? state.Current;

    public override void Start()
    {
        source.Start(Changes);
        state = source.Read();
        Host.Rebuild();
    }

    protected override void Refresh(int news)
    {
        var was = state.Problem;
        state = source.Read();
        // Windows has answered — the mode chosen is in force, or it said no — and from then on what
        // it says is in force is what is marked. A read that lands before the answer changes nothing.
        if (chosen is { } asked && (state.Current == asked || (state.Problem is not null && state.Problem != was)))
        {
            chosen = null;
        }
    }

    private void Choose(ProjectMode mode)
    {
        if (Marked == mode)
        {
            return;
        }
        chosen = mode;
        source.Apply(mode);
        Host.Rebuild();
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        layout.Title(title);
        layout.Gap(2);
        var marked = Marked;
        foreach (var mode in ProjectModes.All)
        {
            var row = rows[mode];
            row.Selected = marked == mode;
            row.Strong = marked == mode;
            layout.Row(row, context.T(40));
            layout.Gap(2);
        }
        if (state.Displays == 1)
        {
            layout.Gap(4);
            Text(layout, alone, layout.TextLeft, layout.TextRight);
        }
        if (state.Problem is { Length: > 0 } trouble)
        {
            problem.Text = trouble;
            layout.Gap(4);
            Text(layout, problem, layout.TextLeft, layout.TextRight);
        }
        Footer(layout, S.DockQuickProjectSettings, "ms-settings:display");
    }

    public override void Dispose() => source.Dispose();
}
