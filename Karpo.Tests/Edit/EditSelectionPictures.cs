// edit: the widget clicked while the bar is arranged is the selected one (the person, 2026-10-05:
// "it should also show the widget as selected"): its block wears the accent's ring, 2 px on its
// edge, as Windows 11 rings a selected item, until another widget is clicked or edit mode ends
// (EditView.Selected, Dock.EditPick). On the dock's real scene with real widgets — the bar a new
// config gets — shown on a desktop of its own and captured as the compositor draws it
// (WindowShots), to KARPO_PNG_DIR as edit-selected-<state>-<look>-<scale>.png: the clock selected,
// the clock selected with the keyboard's focus on it too (the focus rectangle goes over the ring),
// the weather selected with the keyboard's focus on the clock, and nothing selected, for comparing.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void SelectedSheets()
    {
        WindowShots.OnHiddenDesktop("edit-selected", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var area = new RECT { left = 0, top = 0, right = (int)(1920 * dpi / 96), bottom = (int)(dpi * 10) };
                    var (specs, islands) = Made(gfx, config, Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch));
                    try
                    {
                        var content = new Content { Islands = islands };
                        var edit = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content, editing: true);
                        var clock = specs.FindIndex(spec => spec.Kind == BarKind.FolderList);
                        var weather = specs.FindIndex(spec => spec.Kind == BarKind.WidgetsBoard);
                        Assert.True(clock >= 0 && edit.Placed(clock), "the default bar has no clock to select");
                        Assert.Equal(clock, EditClicks.SelectedPart(specs[clock].Key, specs.Select(spec => spec.Key).ToList()));
                        Shoot(gfx, edit, look, new EditView(), $"edit-selected-none-{name}.png", content);
                        Shoot(gfx, edit, look, new EditView(Selected: clock), $"edit-selected-clock-{name}.png", content);
                        Shoot(gfx, edit, look, new EditView(Focused: clock, Selected: clock), $"edit-selected-clock-focused-{name}.png", content);
                        if (weather >= 0 && edit.Placed(weather))
                        {
                            Shoot(gfx, edit, look, new EditView(Focused: clock, Selected: weather), $"edit-selected-weather-focus-clock-{name}.png", content);
                        }
                    }
                    finally
                    {
                        foreach (var island in islands.Values)
                        {
                            island.Dispose();
                        }
                    }
                }
            }
        });
    }
}
