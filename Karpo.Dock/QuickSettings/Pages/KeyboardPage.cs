// The keyboard layouts as a line of a list folder (QuickPageTile, docs\widgets.md §124): the input
// indicator's flyout, which went with the keyboard entry when it became the keyboard preset
// (2026-10-11, "all the remaining flyouts should become folders with a face now").
//
// A row per layout installed, in the order Windows lists them, as Windows' input switcher shows
// them: the language's name over the indicator's codes and the layout's keyboard ("English (United
// States)" over "ENG US · US keyboard"), the one the window the person types in uses marked as a
// quick-settings list marks its chosen row. A click switches that window to the layout and gives it
// the keys back, which closes the folder, as a pick in Windows' switcher closes it; the one in use,
// clicked, does the same, so a click always ends where the person was typing.
//
// What it reads is read as the folder opens (Start), off nothing: the layouts are installed and
// removed in Settings, which the folder is never open over. The face beside it follows a switch made
// here as it follows Win+Space, through the dock's shell hook (KeyboardIsland).

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class KeyboardPage : QuickPage
{
    /// <summary>Segoe Fluent Icons: the keyboard, each row's glyph as the entry's was.</summary>
    private const string Glyph = "";

    private readonly IKeyboardSource source;
    private readonly Keyed<ListRow> rows = new();
    private KeyboardState state = new([], 0);

    public KeyboardPage(IQuickHost host, IKeyboardSource source)
        : base(host)
    {
        this.source = source;
    }

    public override QuickKind Kind => QuickKind.Keyboard;

    /// <summary>The layouts as last read, the one in use among them: a test's view.</summary>
    internal KeyboardState State => state;

    public override void Start()
    {
        state = source.Read();
        Host.Rebuild();
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        layout.Gap(4);
        foreach (var each in state.Layouts)
        {
            var chosen = each.Layout;
            var row = rows.Get($"{chosen:X}", () => new ListRow { Glyph = Glyph, Name = each.Language, Clicked = () => Choose(chosen) });
            row.Title = each.Language;
            row.Detail = each.Detail;
            row.Selected = chosen == state.Current;
            row.Strong = chosen == state.Current;
            layout.Row(row, context.T(52));
            layout.Gap(2);
        }
        rows.Done();
    }

    /// <summary>The window the person types in takes <paramref name="layout"/>, and the folder goes.</summary>
    private void Choose(nint layout)
    {
        source.Choose(layout);
        state = state with { Current = layout };
        Host.Rebuild();
        Host.Close();
    }

    public override void Dispose() => source.Dispose();
}
