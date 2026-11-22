// §625 on the dock's real scene (the person, 2026-11-09: "the ghost preview is a little opaque, this should not be the case.";
// "when having the light system some colors are not actually updated like the ghost preview background color for example.
// (or when dragging a widget)"): every edit visual of a bar takes its colours from the taskbar's look — the mode Karpo's
// Appearance says (§613), Windows' by default — and follows it as it turns, Windows' mode injected, never Windows' own touched.
// A block carried off its bar takes its bar's ground with it (the carry window draws it over the dim, black in every mode);
// the add page's ghost of a pick stands whole, outlined, no wash over it; the empty ghost its washed room alone.
//
// Needs a compositor: KARPO_UI_TESTS=1, as the other scene tree tests.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void TheEditVisualsWearTheTaskbarsModeAndFollowItAsItTurns()
    {
        var config = new Config();
        // Windows as the dock reads it, each mode: injected.
        var (darkWindows, lightWindows) = (AddListPictures.LookFor(dark: true), AddListPictures.LookFor(dark: false));
        Look Bar(Look windows) => windows.Wearing(config) with { Animations = false };
        OnABar(48, AppsBetween(), (layout, scene) =>
        {
            const int widget = 2;
            void Holds(Look bar, string when)
            {
                var tokens = Scene.Theme(bar, layout.Stroke, layout.Border);

                // Carried: on its bar's own ground, in the mode the bar is in.
                scene.Edit(new EditView(Dragging: widget, DropIndex: layout.DropSlot(0f, widget), Away: true));
                Assert.True(scene.Carried() is not null, when);
                Assert.Equal((when, (Windows.UI.Color?)tokens.Base), (when, scene.CarriedGround));

                // The add page's empty ghost: its room washed in the mode's accent, none of the widget drawn.
                scene.Edit(new EditView(Ghost: widget, GhostEmpty: true));
                Assert.Null(scene.CarriedGround);
                Assert.Equal((when, (Windows.UI.Color?)tokens.ZoneTargetFill), (when, scene.GhostWash));
                Assert.Equal(0f, scene.Shows(widget));

                // A pick's ghost: whole, as it will stand, its outline alone over it.
                scene.Edit(new EditView(Ghost: widget));
                Assert.Equal([layout.BlockRect(widget)], scene.GhostsShown);
                Assert.Null(scene.GhostWash);
                Assert.Equal(1f, scene.Shows(widget));
                scene.Edit(new EditView());
            }

            Holds(Bar(darkWindows), "Windows dark");
            // Windows turns light: the same scene, every edit visual in the light look.
            scene.Update(layout, new Content(), Bar(lightWindows), new Icons(), shown: true, new EditView());
            Holds(Bar(lightWindows), "Windows light");
            // Karpo's own Mode Light under a dark Windows (§613): light all the same.
            Assert.True(Looks.WriteMode(config, TaskbarMode.Light));
            scene.Update(layout, new Content(), Bar(darkWindows), new Icons(), shown: true, new EditView());
            Assert.False(Bar(darkWindows).Dark);
            Holds(Bar(darkWindows), "Mode Light, Windows dark");
            // And back to dark, pinned.
            Assert.True(Looks.WriteMode(config, TaskbarMode.Dark));
            scene.Update(layout, new Content(), Bar(lightWindows), new Icons(), shown: true, new EditView());
            Assert.True(Bar(lightWindows).Dark);
            Holds(Bar(lightWindows), "Mode Dark, Windows light");
        }, look: Bar(darkWindows), style: BarStyle.Islands, tiles: 3);
    }
}
