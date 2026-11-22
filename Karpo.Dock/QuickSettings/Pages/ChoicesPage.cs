// A switch's choices as a line of a list folder (QuickPageTile, docs\widgets.md §125): the VPN's
// connections, which the VPN entry's click listed in a flyout until the entry became the VPN preset
// (2026-10-11, "all the remaining flyouts should become folders with a face now").
//
// The page is the switch's own list (IQuickSwitch.Choices) under the switch's name, as Windows'
// quick settings page for it is headed: a row per choice, the switch's glyph before it, what it is
// and where it stands under it; the one in use marked as a quick-settings list marks its chosen row.
// A row that only says something ("No VPN connections set up") is drawn quiet and does nothing, with a
// standard "Add" beside it where the switch says where one is added (SwitchChoice.Add, §404). A
// click hands the row to the switch (IQuickSwitch.Choose), which asks Windows off the dock's thread
// and says when it changed; the folder stays, as Windows' page stays, so the row's "Connecting…"
// and then "Connected" are seen. The switch's settings are the preset's footer under the line.
//
// The switch is the page's own, made as the folder opens and let go as it closes (QuickSwitches.For:
// a test's or the samples on a run that may not reach the person's world).

using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class ChoicesPage : QuickPage
{
    /// <summary>The switch's own news: what it stands for changed.</summary>
    private const int Changed = 1;

    private readonly QuickKind kind;
    private readonly IQuickSwitch source;
    private readonly string glyph;
    private readonly Label title;
    private readonly Keyed<ListRow> rows = new();
    private readonly Button add = new(S.SettingsCommonAdd) { Name = S.SettingsCommonAdd };

    public ChoicesPage(IQuickHost host, QuickKind kind, QuickSwitch which, IQuickSwitch source)
        : base(host)
    {
        this.kind = kind;
        this.source = source;
        glyph = WidgetInfos.For(which).Glyph;
        title = new Label(source.Name, LabelStyle.Strong) { Name = source.Name };
    }

    public override QuickKind Kind => kind;

    /// <summary>The rows as last built: a test's view.</summary>
    internal IReadOnlyList<SwitchChoice> Shown { get; private set; } = [];

    public override void Start()
    {
        source.Start(() => Host.Notify(Changed));
        Host.Rebuild();
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        var choices = source.Choices ?? [];
        Shown = choices;
        if (layout.Lined && !choices.Any(choice => choice.Enabled))
        {
            // Nothing to choose: the kit's one Status (§401), one row with what it says ("No VPN connections set
            // up"), quiet — not the switch's title over it —, and where one is added a standard "Add" against the
            // trailing gutter (§404), which opens that page as the folder goes.
            var first = choices.FirstOrDefault();
            var said = first?.Text ?? S.DockQuickVpnNone;
            var none = rows.Get($"none:{said}", () => new ListRow { Name = said, Glyph = glyph });
            none.Title = said;
            none.Enabled = false;
            none.Clicked = null;
            if (first?.Add is { } where)
            {
                add.Clicked = () => Host.Launch(where);
                layout.Alone(none, add);
            }
            else
            {
                layout.Alone(none);
            }
            rows.Done();
            return;
        }
        layout.Title(title);
        layout.Gap(2);
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[index];
            var at = index;
            var row = rows.Get($"{index}:{choice.Text}", () => new ListRow { Name = choice.Text, Glyph = glyph });
            row.Title = choice.Text;
            row.Detail = choice.Detail;
            row.Enabled = choice.Enabled;
            row.Clicked = choice.Enabled ? () => Choose(at) : null;
            row.Selected = choice.Checked == true;
            row.Strong = choice.Checked == true;
            layout.Row(row, context.T(choice.Detail is { Length: > 0 } ? 52 : 40));
            layout.Gap(2);
        }
        rows.Done();
    }

    private void Choose(int index)
    {
        source.Choose(index);
        Host.Rebuild();
    }

    public override void Dispose() => source.Dispose();
}
