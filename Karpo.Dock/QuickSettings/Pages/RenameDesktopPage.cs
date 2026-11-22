// Renaming a virtual desktop, from the desktop buttons' menu: a text box with
// the desktop's name selected, Save and Cancel. Enter saves, Escape closes. The
// name goes to Windows the way Task View sets it, and Windows keeps it.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class RenameDesktopPage : QuickPage
{
    private readonly IDesktopNames names;
    private readonly Guid desktop;
    private readonly Label title = new(S.DockQuickRenameTitle, LabelStyle.Strong);
    private readonly TextField name = new(maxLength: 100) { Name = S.DockQuickRenameName, Placeholder = S.DockQuickRenamePlaceholder };
    private readonly Label status = new("", LabelStyle.Critical, lines: 2);
    private readonly Button save = new(S.DockQuickSave, ButtonStyle.Accent) { Name = "Save" };
    private readonly Button cancel = new(S.DockQuickCancel) { Name = "Cancel" };

    public RenameDesktopPage(IQuickHost host, IDesktopNames names, Guid desktop)
        : base(host)
    {
        this.names = names;
        this.desktop = desktop;
        save.Clicked = Save;
        name.Submitted = Save;
        cancel.Clicked = Host.Close;
        if (names.NameOf(desktop) is { } current)
        {
            name.Text = current;
            name.SelectAll();
            host.Focus(name);
        }
        else
        {
            name.Enabled = false;
            save.Enabled = false;
            status.Text = S.DockQuickRenameGone;
        }
    }

    public override QuickKind Kind => QuickKind.RenameDesktop;

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        layout.Title(title);
        var left = layout.TextLeft;
        var right = layout.TextRight;
        layout.Gap(2);
        layout.Place(name, left, right - left, context.T(32));
        if (status.Text.Length > 0)
        {
            layout.Gap(8);
            Text(layout, status, left, right);
        }
        layout.Gap(16);
        layout.Buttons(right, cancel, save);
        layout.Gap(8);
    }

    private void Save()
    {
        var typed = name.Text.Trim();
        if (typed.Length == 0)
        {
            status.Text = S.DockQuickRenameEmpty;
            Host.Rebuild();
            return;
        }
        if (names.Rename(desktop, typed))
        {
            Host.Close();
            return;
        }
        status.Text = S.DockQuickRenameRefused;
        Host.Rebuild();
    }
}
